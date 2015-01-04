using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace DynaMut
{
    class ProjectConfigFile
    {
        private const string mRootString = "ProjectConfiguration";
        public Collection<string> mIncludeDirectories;
        private const string mIncludeDirectoryString = "IncludeAbsoluteDirectory";
        public Collection<string> mIncludeFileExtensions;
        private const string mIncludeFileExtensionString = "IncludeFileExtension";
        public Collection<string> mExcludeDirectories;
        private const string mExcludeDirectoryString = "ExcludeDirectory";
        public Collection<string> mExcludeFiles;
        private const string mExcludeFileString = "ExcludeFile";
        public int mNumberOfWorkerThreads;
        private const string mNumberOfWorkerThreadsString = "WorkerThreadsCount";
        public string mBackupFileExtension;
        private const string mBackupFileExtensionString = "BackupFileExtension";

        private static string mConfigFileName = "ProjectConfig.xml";

        public ProjectConfigFile()
        {
            mIncludeDirectories = new Collection<string>();
            mIncludeFileExtensions = new Collection<string>();
            mExcludeDirectories = new Collection<string>();
            mExcludeFiles = new Collection<string>();
            mNumberOfWorkerThreads = 1;
            mBackupFileExtension = "";

            try
            {
                if (File.Exists(mConfigFileName))
                {
                    ReadConfigFile();
                }
                else
                {
                    CreateConfigFile();
                }
            }
            catch (Exception e)
            {
                UI.ShowMessage("Exception: " + e.Message);
            }
        }

        private void ReadConfigFile()
        {
            UI.ShowMessage("Reading project configuration from " + mConfigFileName);
            XmlTextReader xmlReader = new XmlTextReader(mConfigFileName);
            string elementName = "";
            while (xmlReader.Read())
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        elementName = xmlReader.Name;
                        break;
                    case XmlNodeType.Text:
                        switch (elementName)
                        {
                            case mIncludeDirectoryString:
                                mIncludeDirectories.Add(xmlReader.Value);
                                break;
                            case mIncludeFileExtensionString:
                                mIncludeFileExtensions.Add(xmlReader.Value.TrimStart('*'));
                                break;
                            case mExcludeDirectoryString:
                                mExcludeDirectories.Add(xmlReader.Value.Trim('*'));
                                break;
                            case mExcludeFileString:
                                mExcludeFiles.Add(xmlReader.Value.Trim('*'));
                                break;
                            case mNumberOfWorkerThreadsString:
                                mNumberOfWorkerThreads = Int16.Parse(xmlReader.Value);
                                break;
                            case mBackupFileExtensionString:
                                mBackupFileExtension = xmlReader.Value;
                                break;
                        }
                        break;
                }
            }
        }

        private void CreateConfigFile()
        {
            UI.ShowMessage("Creating example project configuration file: " + mConfigFileName + "\nPlease edit it with values for your project.");
            XmlTextWriter xmlWriter = new XmlTextWriter(mConfigFileName, null);
            xmlWriter.Formatting = Formatting.Indented;
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(mRootString);
            xmlWriter.WriteStartElement(mIncludeDirectoryString);
            xmlWriter.WriteString("C:\\Temp");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement(mIncludeFileExtensionString);
            xmlWriter.WriteString("*.cpp");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement(mIncludeFileExtensionString);
            xmlWriter.WriteString("*.c");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement(mExcludeDirectoryString);
            xmlWriter.WriteString("bin");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement(mExcludeFileString);
            xmlWriter.WriteString("test.cpp");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement(mExcludeFileString);
            xmlWriter.WriteString("gui*");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Flush();
            xmlWriter.Close();
        }
    }
}
