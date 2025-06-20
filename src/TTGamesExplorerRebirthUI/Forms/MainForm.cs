using DarkUI.Controls;
using DarkUI.Forms;
using System.Diagnostics;
using System.Security.Cryptography;

namespace TTGamesExplorerRebirthUI.Forms
{
    public partial class MainForm : DarkForm
    {
        private readonly string _loaderExePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "bin", "TTGamesExplorerRebirthLoader.exe");
        private readonly string _bootstrapDllPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "bin", "TTGamesExplorerRebirthBootstrap.dll");
        private readonly string _hookDllPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "bin", "TTGamesExplorerRebirthHook.dll");

        private string _gameExePath;
        private string _loadedGameFolderPath = "";
        private GamesMetadata _gameMetadata;
        private ProcessManager _processManager;

        public MainForm(string path = null)
        {
            InitializeComponent();

            splitContainer3.Panel2Collapsed = true;
            splitContainer3.Panel2.Hide();

            if (path != null)
            {
                if (Directory.Exists(path))
                {
                    AppSettings.Instance.GameFolderPath = path;

                    return;
                }

                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    Helper.OpenFileInternal(path, Path.GetFullPath(path));
                }).Start();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            Helper.EnableDarkModeTitle(Handle);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            AppSettings.Load();

            if (AppSettings.Instance.GameFolderPath != null)
            {
                if(Directory.Exists(AppSettings.Instance.GameFolderPath)){
                    LoadGameFolder(AppSettings.Instance.GameFolderPath);
                } else{
                    AppSettings.Instance.GameFolderPath = null;
                }
            }

            keepHookLogsOpenToolStripMenuItem.Checked = AppSettings.Instance.KeepLogsOpen;
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void LoadGameFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                LoadGameFolder(folderBrowserDialog1.SelectedPath);

                AppSettings.Instance.GameFolderPath = folderBrowserDialog1.SelectedPath;

                AppSettings.Save();
            }
        }

        private void OpenFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Helper.OpenFileInternal(Path.GetDirectoryName(openFileDialog1.FileName), openFileDialog1.FileName);
            }
        }

        private void LoadGameFolder(string path)
        {
            LoadFolderInTreeView(path);

            if (_gameMetadata != null)
            {
                toolStripStatusLabel1.Text = $"{_gameMetadata.Name} - {_loadedGameFolderPath}";
            }
            else
            {
                toolStripStatusLabel1.Text = "Ready";
            }

            _processManager = new ProcessManager(_gameExePath, _loaderExePath);

            _processManager.GameStateChanged += ProcessManager_GameStateChanged;
        }

        private readonly Bitmap _folderBitmap = new Bitmap(Properties.Resources.folder);
        private readonly Bitmap _expandedBitmap = new Bitmap(Properties.Resources.folder_page);

        private void LoadFolderInTreeView(string folderPath)
        {
            Invoke(new Action(darkTreeView1.Nodes.Clear));

            Invoke((MethodInvoker)(() => splitContainer2.Panel2Collapsed = true));

            _loadedGameFolderPath = folderPath;

            DarkTreeNode rootNode = new("Root")
            {
                ExpandedIcon = _expandedBitmap,
                Icon = _folderBitmap,
                ExpandAreaHot = true,
                IsRoot = true,
                Expanded = true,
            };

            DarkTreeNode parentNode = null;
            DarkTreeNode tempRootNode = null;
            string subPathRoot;

            foreach (string path in Directory.EnumerateDirectories(folderPath, "*.*", SearchOption.AllDirectories))
            {
                subPathRoot = "Root\\";

                parentNode ??= rootNode;

                if (tempRootNode != null)
                {
                    parentNode = tempRootNode;
                    tempRootNode = null;
                }

                var pathSplit = path.Replace(folderPath, "").Split("\\").Skip(1).ToArray();
                var i = 0;
                var count = pathSplit.Length;
                foreach (string subPath in pathSplit)
                {
                    if (subPath.Trim() != "")
                    {
                        subPathRoot += subPath + "\\";

                        DarkTreeNode current = parentNode.Nodes.Find(node => node.FullPath == subPathRoot.TrimEnd('\\'));
                        if (current == null)
                        {
                            DarkTreeNode node = new(subPath)
                            {
                                ExpandedIcon = _expandedBitmap,
                                Icon = _folderBitmap,
                                Expanded = true,
                            };

                            parentNode.Nodes.Add(node);

                            parentNode = node;
                        }
                        else
                        {
                            tempRootNode = parentNode;
                            parentNode = current;
                        }
                    }

                    if (++i == count)
                    {
                        parentNode = rootNode;
                        tempRootNode = null;
                    }
                }
            }

            Invoke(new Action(() =>
            {
                darkTreeView1.Nodes.Add(rootNode);
                darkTreeView1.SelectNode(rootNode);
            }));

            // Populate game informations.
            foreach (string path in Directory.EnumerateFiles(folderPath, "*.exe"))
            {
                FileStream stream = File.OpenRead(path);
                string sha1 = BitConverter.ToString(SHA1.Create().ComputeHash(stream)).Replace("-", "");

                _gameMetadata = GamesMetadataHelper.Items.Where(metadata => metadata.Hash == sha1).FirstOrDefault();
                if (_gameMetadata != null)
                {
                    Invoke((MethodInvoker)(() =>
                    {
                        pictureBox1.Image = _gameMetadata.Cover;
                        splitContainer2.SplitterDistance = splitContainer1.Height - 360;
                        splitContainer2.Panel2Collapsed = false;
                    }));

                    _gameExePath = Path.Combine(_loadedGameFolderPath, _gameMetadata.Exe);

                    break;
                }
            }

            // Sets the FileSystemWatcher.
            FileSystemWatcher watcher = new()
            {
                Path = folderPath,
                IncludeSubdirectories = true,
                NotifyFilter =
                    NotifyFilters.Attributes |
                    NotifyFilters.CreationTime |
                    NotifyFilters.DirectoryName |
                    NotifyFilters.FileName |
                    NotifyFilters.LastAccess |
                    NotifyFilters.LastWrite |
                    NotifyFilters.Security |
                    NotifyFilters.Size,
                Filter = "*.*",
            };

            // TODO: Handle folder browsing when refresh. (Example: Delete a file in an expanded subfolder or delete an expanded subfolder)
            // FIXME: In some cases, it seems to call this twice, so there is 2 "Root" folder.
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            watcher.EnableRaisingEvents = true;
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            LoadFolderInTreeView(_loadedGameFolderPath);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            LoadFolderInTreeView(_loadedGameFolderPath);
        }

        private void DarkTreeView1_SelectedNodesChanged(object sender, EventArgs e)
        {
            if (darkTreeView1.SelectedNodes.Count == 0)
            {
                return;
            }

            darkTreeView1.SelectedNodes[0].Expanded = true;

            darkListView1.Items.Clear();

            if (darkTreeView1.SelectedNodes[0].FullPath != "Root")
            {
                string parent = Path.GetDirectoryName(darkTreeView1.SelectedNodes[0].FullPath);

                darkListView1.Items.Add(new DarkListItem("..")
                {
                    Icon = new Bitmap(Properties.Resources.arrow_up),
                    Tag = parent,
                });
            }

            foreach (string path in Directory.EnumerateFileSystemEntries(Path.GetFullPath(Path.Join(_loadedGameFolderPath, darkTreeView1.SelectedNodes[0].FullPath[4..])), "*.*").OrderBy(File.GetAttributes))
            {
                DarkListItem listItem = new(Path.GetFileName(path))
                {
                    Tag = path
                };

                if (File.GetAttributes(path) == FileAttributes.Directory)
                {
                    listItem.Icon = _folderBitmap;
                }
                else
                {
                    listItem.Icon = Helper.GetIconByFileName(path);
                    listItem.Text += $" ({Helper.FormatSize((ulong)new FileInfo(path).Length)})";
                }

                darkListView1.Items.Add(listItem);
            }

            if (_gameMetadata != null)
            {
                toolStripStatusLabel1.Text = $"{_gameMetadata.Name} - {_loadedGameFolderPath}";
            }
            else
            {
                toolStripStatusLabel1.Text = "Ready";
            }

            if (darkTreeView1.SelectedNodes[0].FullPath == "Root")
            {
                darkSectionPanel2.SectionHeader = "File(s)";
            }
            else
            {
                darkSectionPanel2.SectionHeader = $"File(s) of \"{darkTreeView1.SelectedNodes[0].FullPath[5..]}\"";
            }
        }

        private void DarkListView1_DoubleClick(object sender, EventArgs e)
        {
            string path = (string)darkListView1.Items[darkListView1.SelectedIndices[0]].Tag;

            if (Directory.Exists(path) || path[..4] == "Root")
            {
                if (path[..4] != "Root")
                {
                    path = path.Replace(_loadedGameFolderPath, "Root");
                }

                DarkTreeNode parent = null;
                string oldSubPath = "";

                foreach (var subPath in path.Split("\\"))
                {
                    parent ??= darkTreeView1.Nodes[0];

                    DarkTreeNode node;
                    if (oldSubPath == "")
                    {
                        oldSubPath = "Root";
                        node = darkTreeView1.Nodes.Find(item => item.FullPath == oldSubPath + subPath);
                    }
                    else
                    {
                        oldSubPath += "\\" + subPath;
                        node = parent.Nodes.Find(item => item.FullPath == oldSubPath);
                    }

                    if (node != null)
                    {
                        parent = node;
                    }
                }

                if (parent != null)
                {
                    darkTreeView1.SelectNode(parent);
                    parent.Expanded = true;
                }
            }
            else
            {
                // NOTE: Then it's a file. Try to open it.

                Helper.OpenFileInternal(_loadedGameFolderPath, (string)darkListView1.Items[darkListView1.SelectedIndices[0]].Tag);
            }
        }

        private void DarkContextMenu1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (darkListView1.SelectedIndices.Count < 1)
            {
                e.Cancel = true;
            }
            else
            {
                if (!Path.GetExtension((string)darkListView1.Items[darkListView1.SelectedIndices[0]].Tag).Equals(".dat", StringComparison.InvariantCultureIgnoreCase))
                {
                    e.Cancel = true;
                }
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutForm().ShowDialog();
        }

        private void RunToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make sure the loader is killed.
            _processManager.KillAllProcesses();

            textBox1.Text = "";

            Process process = new();

            process.StartInfo.FileName = _loaderExePath;
            process.StartInfo.Arguments = _gameExePath;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
        }

        private void RunWithHooksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make sure the loader is killed.
            _processManager.KillAllProcesses();

            textBox1.Text = "";

            Process process = new();

            process.StartInfo.FileName = _loaderExePath;
            process.StartInfo.Arguments = $"\"{_gameExePath}\" \"{_bootstrapDllPath}\" \"{_hookDllPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;

            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Invoke((MethodInvoker)(() =>
                    {
                        textBox1.AppendText(e.Data + Environment.NewLine);
                    }));
                }
            });

            process.Start();
            process.BeginOutputReadLine();

            splitContainer3.Panel2Collapsed = false;
            splitContainer3.Panel2.Show();
        }

        private void ProcessManager_GameStateChanged(object sender, EventArgs e)
        {
            Invoke((MethodInvoker)(() =>
            {
                if (!_processManager.IsLoaderRunning)
                {
                    if (!keepHookLogsOpenToolStripMenuItem.Checked)
                    {
                        splitContainer3.Panel2Collapsed = true;
                        splitContainer3.Panel2.Hide();
                    }
                }

                if (!_processManager.IsGameRunning && _processManager.IsLoaderRunning)
                {
                    _processManager.KillAllProcesses();

                    if (!keepHookLogsOpenToolStripMenuItem.Checked)
                    {
                        splitContainer3.Panel2Collapsed = true;
                        splitContainer3.Panel2.Hide();
                    }
                }

                runToolStripMenuItem.Enabled = !_processManager.IsGameRunning;
                runWithHooksToolStripMenuItem.Enabled = !_processManager.IsGameRunning;
            }));
        }

        private void ExitRunningGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _processManager.KillAllProcesses();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e){
            _processManager?.Dispose();
        }

        private void toolStripMenuItem1_CheckedChanged(object sender, EventArgs e)
        {
            AppSettings.Instance.KeepLogsOpen = keepHookLogsOpenToolStripMenuItem.Checked;
            AppSettings.Save();
        }

        private void hideHookLogsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            splitContainer3.Panel2Collapsed = true;
            splitContainer3.Panel2.Hide();
        }
    }
}
