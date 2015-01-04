using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DynaMut
{
    class MutationEngine
    {
        public const string MUTATION_INDEX_STRING = "MUTATION_INDEX";
        private MutationConfigFile mConfigFile;
        private Project mProject;

        public MutationEngine(Project project)
        {
            mConfigFile = new MutationConfigFile();
            mProject = project;
        }

        public void MutateFiles()
        {
            ManualResetEvent[] doneEvents = new ManualResetEvent[mProject.mCodeFiles.Count];
            FileMutator[] fileMutators = new FileMutator[mProject.mCodeFiles.Count];
            ThreadPool.SetMinThreads(mProject.GetNumberOfWorkerThreads(), mProject.GetNumberOfWorkerThreads());
            bool success = ThreadPool.SetMaxThreads(mProject.GetNumberOfWorkerThreads(), mProject.GetNumberOfWorkerThreads());
            for (int i = 0; i < mProject.mCodeFiles.Count; ++i)
            {
                string filePath = mProject.mCodeFiles[i];
                doneEvents[i] = new ManualResetEvent(false);
                fileMutators[i] = new FileMutator(filePath, doneEvents[i], mConfigFile.mMutationList);
                try
                {
                    ThreadPool.QueueUserWorkItem(fileMutators[i].MutateFile, new StateObject(i, mProject.mCodeFiles.Count));
                }
                catch (NotSupportedException)
                {
                    // threading must not be supported, do it the old fashioned way.
                    fileMutators[i].MutateFile(new StateObject(i, mProject.mCodeFiles.Count));
                }
            }

            WaitForMany(doneEvents);
            int currentMutationIndex = 0;
            foreach (FileMutator fileMutator in fileMutators)
            {
                if (fileMutator.mCurrentMutationIndex > 0)
                {
                    string fileContents = fileMutator.OpenAndReadCodeFile();
                    int index = fileContents.LastIndexOf("#include");
                    index += fileContents.Substring(index).IndexOf('\n');
                    fileContents = fileContents.Substring(0, index + 1) + "#define " + MUTATION_INDEX_STRING + "(offset) (offset + " + currentMutationIndex + ")\r\n" + fileContents.Substring(index + 1);
                    fileMutator.SaveToFile(fileContents);
                    currentMutationIndex += fileMutator.mCurrentMutationIndex;
                }
            }

            UI.ShowMessage("Total number of mutations: " + currentMutationIndex);
        }     

        private void WaitForMany(ManualResetEvent[] doneEvents)
        {
            foreach (ManualResetEvent doneEvent in doneEvents)
            {
                doneEvent.WaitOne();
            }
        }
    }

    class StateObject
    {
        public int mIndex;
        public int mTotalFiles;
        public StateObject(int index, int totalFiles)
        {
            mIndex = index;
            mTotalFiles = totalFiles;
        }
    }

    class FileMutator
    {
        private const string LEFT_HAND_SIDE_ID_STRING = "lhs";
        private const string RIGHT_HAND_SIDE_ID_STRING = "rhs";
        private const string OPERAND_ID_STRING = "operand";
        private const string OPERATOR_ID_STRING = "operator";

        public int mCurrentMutationIndex;
        private cMutationGroupInfo mCurrentMutation;
        private ManualResetEvent mDoneEvent;
        public string mFilePath;
        private List<cMutationGroupInfo> mMutationList;

        public FileMutator(string filePath, ManualResetEvent doneEvent, List<cMutationGroupInfo> mutationList)
        {
            mCurrentMutationIndex = 0;
            mCurrentMutation = null;
            mDoneEvent = doneEvent;
            mFilePath = filePath;
            mMutationList = mutationList;
        }
        
        public void MutateFile(Object state)
        {
            StateObject stateObj = (StateObject)state;
            UI.ShowMessage("Starting to convert file " + (stateObj.mIndex + 1) + " of " + stateObj.mTotalFiles + " at " + DateTime.Now.TimeOfDay);
            string fileContents = OpenAndReadCodeFile();
            foreach (cMutationGroupInfo mutation in mMutationList)
            {
                mCurrentMutation = mutation;
                // set up the match evaluator delegate based on mutation type
                MatchEvaluator evaluatorDelegate;
                switch (mutation.mutationType)
                {
                    case eMutationType.OperatorReplacementBinary:
                        evaluatorDelegate = new MatchEvaluator(ReplaceOrb);
                        break;
                    case eMutationType.OperatorReplacementUnary:
                        evaluatorDelegate = new MatchEvaluator(ReplaceOru);
                        break;
                    case eMutationType.LiteralValueReplacement:
                        evaluatorDelegate = new MatchEvaluator(ReplaceLvr);
                        break;
                    default:
                        evaluatorDelegate = null;
                        UI.ShowMessage("Unexpected mutation type");
                        break;
                }

                if (mutation.mutationType == eMutationType.LiteralValueReplacement)
                {
                    fileContents = mutation.regexObj.Replace(fileContents, evaluatorDelegate);
                }
                else
                {
                    // Some code statements will have multiple instances of a 
                    // given operator.  The Replace must be run in a loop to 
                    // catch all of these.  Also only allow 6 loops to prevent
                    // infinite looping.
                    int lastMutationIndex = -1;
                    int count = 0;
                    if (!ContainsOperators(fileContents, mutation))
                    {
                        // the Replace can hang in certain files
                        continue;
                    }
                    while (mCurrentMutationIndex > lastMutationIndex && count <= 5)
                    {
                        lastMutationIndex = mCurrentMutationIndex;
                        fileContents = mutation.regexObj.Replace(fileContents, evaluatorDelegate);
                        ++count;
                    }
                }
            }

            UI.ShowMessage(mCurrentMutationIndex + " mutations added to file " + mFilePath);
            SaveToFile(fileContents);
            mDoneEvent.Set();
        }

        private bool ContainsOperators(string fileContents, cMutationGroupInfo mutGroup)
        {
            bool contains = false;
            foreach (cMutationMemberInfo member in mutGroup.members)
            {
                if (fileContents.Contains(" " + member.operatorStr + " ") && member.operatorStr != "*")
                {
                    contains = true;
                }
            }
            return contains;
        }

        public string OpenAndReadCodeFile()
        {
            string fileContents = "";
            if (File.Exists(mFilePath))
            {
                fileContents = File.ReadAllText(mFilePath);
            }

            return fileContents;
        }

        public void SaveToFile(string contents)
        {
            FileAttributes attributes = File.GetAttributes(mFilePath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(mFilePath, FileAttributes.Normal);
            }
            File.WriteAllText(mFilePath, contents);
        }

        private string ReplaceOrb(Match m)
        {
            if (IsOrbMatchInComment(m))
            {
                // remove operator in comment to improve performance
                // if a comment has a diagram with a lot of symbols, this can really slow everything down
                return m.Groups[LEFT_HAND_SIDE_ID_STRING].Value + m.Groups[RIGHT_HAND_SIDE_ID_STRING].Value;
            }
            else if (IsOrbMatchInString(m))
            {
                return m.Value;
            }
            LeftOperand lhs = ParseLeftOperand(m.Groups[LEFT_HAND_SIDE_ID_STRING].Value);
            RightOperand rhs = ParseRightOperand(m.Groups[RIGHT_HAND_SIDE_ID_STRING].Value);
            if (IsSubtractingPointers(m, lhs, rhs) || IsAddingStrings(m, lhs, rhs))
            {
                // two pointers can be subtracted, but not added, so do not mutate subtraction of pointers
                // two strings/chars can be added, but not subtracted, so do not mutate addition of strings
                return m.Value;
            }
            cMutationMemberInfo currentMember = mCurrentMutation.GetMember(m.Groups[OPERATOR_ID_STRING].Value);
            string replacement = lhs.preOperand + currentMember.replacementFunction + "(" + lhs.operand + ", " + rhs.operand + ", " + MutationEngine.MUTATION_INDEX_STRING + "(" + mCurrentMutationIndex + "))" + rhs.postOperand;
            mCurrentMutationIndex += currentMember.numOfMutations;
            return replacement;
        }

        private string ReplaceOru(Match m)
        {
            cMutationMemberInfo currentMember = mCurrentMutation.GetMember(m.Groups[OPERATOR_ID_STRING].Value);
            string replacement = currentMember.replacementFunction + "(" + m.Groups[OPERAND_ID_STRING].Value + ", " + MutationEngine.MUTATION_INDEX_STRING + "(" + mCurrentMutationIndex + "))";
            mCurrentMutationIndex += currentMember.numOfMutations;
            return replacement;
        }

        private string ReplaceLvr(Match m)
        {
            if (IsOrbMatchInComment(m))
            {
                // remove operator in comment to improve performance
                // if a comment has a diagram with a lot of symbols, this can really slow everything down
                return m.Groups[LEFT_HAND_SIDE_ID_STRING].Value + m.Groups[RIGHT_HAND_SIDE_ID_STRING].Value;
            }
            else if (IsOrbMatchInString(m))
            {
                return m.Value;
            }
            cMutationMemberInfo currentMember = mCurrentMutation.GetMember(m.Groups[OPERATOR_ID_STRING].Value);
            string replacement = m.Groups[LEFT_HAND_SIDE_ID_STRING].Value + " " + m.Groups[OPERATOR_ID_STRING].Value + " " + currentMember.replacementFunction + "(" + m.Groups[RIGHT_HAND_SIDE_ID_STRING].Value + ", " + MutationEngine.MUTATION_INDEX_STRING + "(" + mCurrentMutationIndex + "))";
            mCurrentMutationIndex += currentMember.numOfMutations;
            return replacement;
        }

        private bool IsOrbMatchInComment(Match m)
        {
            string lhs = m.Groups[LEFT_HAND_SIDE_ID_STRING].Value;
            if (lhs.Contains("/*"))
            {
                string subLhs = lhs.Substring(lhs.LastIndexOf("/*"));
                if (!subLhs.Contains("*/"))
                {
                    return true;
                }
            }

            if (lhs.Contains("//"))
            {
                string subLhs = lhs.Substring(lhs.LastIndexOf("//"));
                if (!subLhs.Contains("\n"))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsOrbMatchInString(Match m)
        {
            string lhs = m.Groups[LEFT_HAND_SIDE_ID_STRING].Value;
            int numOfSplits = lhs.Split('\"').Length;
            bool inString = false;
            bool inChar = false;
            if (numOfSplits > 1)
            {
                for (int i = 0; i < lhs.Length; ++i)
                {
                    char currentCh = lhs.ToCharArray()[i];
                    if (inString)
                    {
                        if (currentCh == '"' && lhs.ToCharArray()[i - 1] != '\\')
                        {
                            inString = false;
                        }
                    }
                    else if (inChar)
                    {
                        if (currentCh == '\'' && lhs.ToCharArray()[i - 1] != '\\')
                        {
                            inChar = false;
                        }
                    }
                    else
                    {
                        if (currentCh == '\'')
                        {
                            inChar = true;
                        }
                        else if (currentCh == '"')
                        {
                            inString = true;
                        }
                    }
                }
            }

            return inString;
        }

        private bool IsSubtractingPointers(Match m, LeftOperand lhs, RightOperand rhs)
        {
            if (m.Groups[OPERATOR_ID_STRING].Value != "-")
            {
                return false;
            }

            // assume code follows semi-hungarian style to indicate pointer, member and global variables
            if ((lhs.operand.StartsWith("p") || lhs.operand.StartsWith("mp") || lhs.operand.StartsWith("gp")) &&
                (rhs.operand.StartsWith("p") || rhs.operand.StartsWith("mp") || rhs.operand.StartsWith("gp")))
            {
                return true;
            }

            return false;
        }

        private bool IsAddingStrings(Match m, LeftOperand lhs, RightOperand rhs)
        {
            if (m.Groups[OPERATOR_ID_STRING].Value != "+")
            {
                return false;
            }

            if (lhs.operand.EndsWith("\"") || lhs.operand.EndsWith("'") ||
                rhs.operand.EndsWith("\"") || rhs.operand.EndsWith("'"))
            {
                return true;
            }

            return false;
        }

        struct LeftOperand
        {
            public string preOperand;
            public string operand;
        }

        struct RightOperand
        {
            public string operand;
            public string postOperand;

        }

        private bool IsAnOpenLigature(char ch)
        {
            return ch == '{' || ch == '[' || ch == '(';
        }

        private bool IsACloseLigature(char ch)
        {
            return ch == '}' || ch == ']' || ch == ')';
        }

        private bool IsQuote(char ch)
        {
            return ch == '\"' || ch == '\'';
        }

        private bool IsSeparator(char ch)
        {
            return ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' || ch == ',';
        }

        private LeftOperand ParseLeftOperand(string rawOperand)
        {
            LeftOperand lOperand = new LeftOperand();
            int nestedDepth = 0;
            int separationIndex = -1;
            bool inString = false;
            bool inChar = false;
            for (int i = rawOperand.Length; i > 0; --i)
            {
                char currentCh = rawOperand.ToCharArray()[i - 1];
                if (inString)
                {
                    if (currentCh == '"' && rawOperand.ToCharArray()[i - 2] != '\\')
                    {
                        inString = false;
                    }
                }
                else if (inChar)
                {
                    if (currentCh == '\'' && rawOperand.ToCharArray()[i - 2] != '\\')
                    {
                        inChar = false;
                    }
                }
                else
                {
                    if (currentCh == '"')
                    {
                        inString = true;
                    }
                    else if (currentCh == '\'')
                    {
                        inChar = true;
                    }
                    else if (IsACloseLigature(currentCh))
                    {
                        ++nestedDepth;
                    }
                    else if (IsAnOpenLigature(currentCh))
                    {
                        if (nestedDepth == 0)
                        {
                            // we are not inside braces, this must be a mark containing the operation
                            separationIndex = i;
                            break;
                        }

                        --nestedDepth;
                    }
                    else if (IsSeparator(currentCh))
                    {
                        if (nestedDepth == 0)
                        {
                            // we are not inside braces, whitespace indicates end of operand, due to coding standard
                            separationIndex = i;
                            break;
                        }
                    }
                }
            }

            if (separationIndex > -1)
            {                
                lOperand.operand = rawOperand.Substring(separationIndex);
                lOperand.preOperand = rawOperand.Substring(0, separationIndex);
            }
            else
            {
                lOperand.operand = rawOperand;
                lOperand.preOperand = "";
            }

            return lOperand;
        }

        private RightOperand ParseRightOperand(string rawOperand)
        {
            RightOperand rOperand = new RightOperand();
            int nestedDepth = 0;
            int separationIndex = -1;
            bool inString = false;
            bool inChar = false;
            for (int i = 0; i < rawOperand.Length; ++i)
            {
                char currentCh = rawOperand.ToCharArray()[i];
                if (inString)
                {
                    if (currentCh == '"' && rawOperand.ToCharArray()[i - 1] != '\\')
                    {
                        inString = false;
                    }
                }
                else if (inChar)
                {
                    if (currentCh == '\'' && rawOperand.ToCharArray()[i - 1] != '\\')
                    {
                        inChar = false;
                    } 
                }
                else
                {
                    if (currentCh == '"')
                    {
                        inString = true;
                    }
                    else if (currentCh == '\'')
                    {
                        inChar = true;
                    }
                    else if (IsAnOpenLigature(currentCh))
                    {
                        ++nestedDepth;
                    }
                    else if (IsACloseLigature(currentCh))
                    {
                        if (nestedDepth == 0)
                        {
                            // we are not inside braces, this must be a mark containing the operation
                            separationIndex = i;
                            break;
                        }

                        --nestedDepth;
                    }
                    else if (IsSeparator(currentCh))
                    {
                        if (nestedDepth == 0)
                        {
                            // we are not inside braces, whitespace indicates end of operand, due to coding standard
                            separationIndex = i;
                            break;
                        }
                    }
                }
            }

            if (separationIndex > -1)
            {
                rOperand.operand = rawOperand.Substring(0, separationIndex);
                rOperand.postOperand = rawOperand.Substring(separationIndex);
            }
            else
            {
                rOperand.operand = rawOperand;
                rOperand.postOperand = "";
            }

            return rOperand;
        }
    }
}
