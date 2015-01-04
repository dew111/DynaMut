using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace DynaMut
{
    public enum eMutationType
    {
        OperatorReplacementBinary,
        OperatorReplacementUnary,
        LiteralValueReplacement,
        N_MUTATION_TYPES
    }

    public class cMutationGroupInfo
    {
        public eMutationType mutationType;
        public Regex regexObj;
        public int numOfMembers;
        public List<cMutationMemberInfo> members;

        public cMutationGroupInfo()
        {
            mutationType = eMutationType.N_MUTATION_TYPES;
            regexObj = null;
            numOfMembers = 0;
            members = new List<cMutationMemberInfo>();
        }

        public cMutationMemberInfo GetMember(string operatorStr)
        {
            foreach (cMutationMemberInfo member in members)
            {
                if (operatorStr == member.operatorStr)
                {
                    return member;
                }
            }

            return null;
        }

        public bool IsComplete()
        {
            return mutationType != eMutationType.N_MUTATION_TYPES &&
                   regexObj != null &&
                   numOfMembers == members.Count &&
                   numOfMembers != 0;
        }
    }

    public class cMutationMemberInfo
    {
        public string operatorStr;
        public int numOfMutations;
        public string replacementFunction;

        public cMutationMemberInfo()
        {
            operatorStr = "";
            numOfMutations = 0;
            replacementFunction = "";
        }

        public bool IsComplete()
        {
            return operatorStr != "" &&
                   numOfMutations != 0 &&
                   replacementFunction != "";
        }
    }

    class MutationConfigFile
    {
        private const string mRootString = "MutationConfiguration";
        private const string mOrbTypeString = "OperatorReplacementBinaryGroup";
        private const string mOruTypeString = "OperatorReplacementUnaryGroup";
        private const string mLvrTypeString = "LiteralValueReplacementGroup";
        private const string mGrpMemberString = "GroupMember";
        private const string mOperatorString = "Operator"; 
        private const string mRegexString = "RegularExpression";
        private const string mNumMemberString = "NumberOfMembers";
        private const string mNumMutationsString = "NumberOfMutations";
        private const string mReplacementFunctionString = "ReplacementFunction";

        private const string mConfigFileName = "MutationConfig.xml";

        public List<cMutationGroupInfo> mMutationList;

        public MutationConfigFile()
        {
            mMutationList = new List<cMutationGroupInfo>();

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
            UI.ShowMessage("Reading mutation configuration from " + mConfigFileName);
            XmlTextReader xmlReader = new XmlTextReader(mConfigFileName);
            string elementName = "";
            cMutationGroupInfo mutationGroupInfoTemp = new cMutationGroupInfo();
            cMutationMemberInfo mutationMemberInfoTemp = new cMutationMemberInfo();
            while (xmlReader.Read())
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        elementName = xmlReader.Name;
                        switch (elementName)
                        {
                            case mOrbTypeString:
                                mutationGroupInfoTemp.mutationType = eMutationType.OperatorReplacementBinary;
                                break;
                            case mOruTypeString:
                                mutationGroupInfoTemp.mutationType = eMutationType.OperatorReplacementUnary;
                                break;
                            case mLvrTypeString:
                                mutationGroupInfoTemp.mutationType = eMutationType.LiteralValueReplacement;
                                break;
                        }
                        break;
                    case XmlNodeType.Text:
                        switch (elementName)
                        {
                            case mRegexString:
                                mutationGroupInfoTemp.regexObj = new Regex(xmlReader.Value);
                                break;
                            case mNumMemberString:
                                mutationGroupInfoTemp.numOfMembers = int.Parse(xmlReader.Value);
                                break;
                            case mOperatorString:
                                mutationMemberInfoTemp.operatorStr = xmlReader.Value;
                                break;
                            case mNumMutationsString:
                                mutationMemberInfoTemp.numOfMutations = int.Parse(xmlReader.Value);
                                break;
                            case mReplacementFunctionString:
                                mutationMemberInfoTemp.replacementFunction = xmlReader.Value;
                                break;
                        }

                        if (mutationMemberInfoTemp.IsComplete())
                        {
                            mutationGroupInfoTemp.members.Add(mutationMemberInfoTemp);
                            mutationMemberInfoTemp = new cMutationMemberInfo();
                        }

                        if (mutationGroupInfoTemp.IsComplete())
                        {
                            mMutationList.Add(mutationGroupInfoTemp);
                            mutationGroupInfoTemp = new cMutationGroupInfo();
                        }
                        break;
                }
            }
        }

        private void CreateConfigFile()
        {
            UI.ShowMessage("Creating example mutation configuration file: " + mConfigFileName + "\nPlease edit it with values for your project.");
            XmlTextWriter xmlWriter = new XmlTextWriter(mConfigFileName, null);
            xmlWriter.Formatting = Formatting.Indented;
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement(mRootString);
            //xmlWriter.WriteStartElement(mIncludeDirectoryString);
            xmlWriter.WriteString("C:\\Temp");
            xmlWriter.WriteEndElement();
            //xmlWriter.WriteStartElement(mIncludeFileExtensionString);
            xmlWriter.WriteString("*.cpp");
            xmlWriter.WriteEndElement();
            //xmlWriter.WriteStartElement(mIncludeFileExtensionString);
            xmlWriter.WriteString("*.c");
            xmlWriter.WriteEndElement();
            //xmlWriter.WriteStartElement(mExcludeDirectoryString);
            xmlWriter.WriteString("bin");
            xmlWriter.WriteEndElement();
            //xmlWriter.WriteStartElement(mExcludeFileString);
            xmlWriter.WriteString("test.cpp");
            xmlWriter.WriteEndElement();
            //xmlWriter.WriteStartElement(mExcludeFileString);
            xmlWriter.WriteString("gui*");
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Flush();
            xmlWriter.Close();
        }
    }
}
