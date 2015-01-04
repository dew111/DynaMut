using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynaMut
{
    class Project
    {
        public List<string> mCodeFiles;
        public const string RESTORE_BACKUP_FILES_ARGUMENT = "-restoreFiles";

        private ProjectConfigFile mConfigFile;

        public Project()
        {
            mCodeFiles = new List<string>();
            mConfigFile = new ProjectConfigFile();
        }

        public void GenerateCodeFilesList()
        {
            UI.ShowMessage("Generating list of code files.");
            List<string> searchDirectories = new List<string>();
            foreach (string directory in mConfigFile.mIncludeDirectories)
            {
                searchDirectories.Add(directory);
            }

            for (int i = 0; i < searchDirectories.Count; ++i)
            {
                if (Directory.Exists(searchDirectories[i]))
                {
                    foreach (string subDir in Directory.EnumerateDirectories(searchDirectories[i]))
                    {
                        if (IsDirectoryAllowed(subDir))
                        {
                            searchDirectories.Add(subDir);
                        }
                    }

                    foreach (string filePath in Directory.EnumerateFiles(searchDirectories[i]))
                    {
                        if (IsFileAllowed(filePath))
                        {
                            mCodeFiles.Add(filePath);
                        }
                    }
                }
            }

            UI.ShowMessage("Found " + mCodeFiles.Count + " code files.");
        }

        public int GetNumberOfWorkerThreads()
        {
            return mConfigFile.mNumberOfWorkerThreads;
        }

        public void BackupCodeFiles()
        {
            if (IsFileBackupConfigured())
            {
                UI.ShowMessage("Backing up code files.  To restore backup files, run: DynaMut.exe " + RESTORE_BACKUP_FILES_ARGUMENT);
                bool userPrompted = false;
                foreach (string filePath in mCodeFiles)
                {
                    string backupFilePath = filePath + mConfigFile.mBackupFileExtension;
                    if (!userPrompted && File.Exists(backupFilePath))
                    {
                        bool confirm = UI.GetUserConfirmation("Backup file already exists.  Overwrite existing backup files?");
                        if (!confirm)
                        {
                            return;
                        }

                        userPrompted = true;
                    }

                    SetAttributesNormal(backupFilePath);
                    File.Copy(filePath, backupFilePath, true);
                }
            }
        }

        public void RestoreBackupCodeFiles()
        {
            UI.ShowMessage("Restoring backed up code files.");
            foreach (string filePath in mCodeFiles)
            {
                SetAttributesNormal(filePath);
                string backupFilePath = filePath + mConfigFile.mBackupFileExtension;
                File.Copy(backupFilePath, filePath, true);
            }
        }

        private void SetAttributesNormal(string filePath)
        {
            if (File.Exists(filePath))
            {
                FileAttributes attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
            }
        }

        private bool IsFileBackupConfigured()
        {
            return mConfigFile.mBackupFileExtension != "";
        }

        private bool IsDirectoryAllowed(string directory)
        {
            bool isAllowed = true;
            foreach (string excludedDir in mConfigFile.mExcludeDirectories)
            {
                if (directory.Contains(excludedDir))
                {
                    isAllowed = false;
                    break;
                }
            }

            return isAllowed;
        }

        private bool IsFileAllowed(string filePath)
        {
            bool isAllowed = false;
            foreach (string includedFileExtension in mConfigFile.mIncludeFileExtensions)
            {
                if (filePath.EndsWith(includedFileExtension))
                {
                    isAllowed = true;
                    break;
                }
            }

            if (isAllowed)
            {
                string fileName = filePath.Split('\\').Last<string>();
                foreach (string excludedFileName in mConfigFile.mExcludeFiles)
                {
                    if (filePath.Contains(excludedFileName))
                    {
                        isAllowed = false;
                        break;
                    }
                }
            }

            return isAllowed;
        }
    }
}
