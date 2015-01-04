using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynaMut
{
    class Program
    {
        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            Project project = new Project();
            project.GenerateCodeFilesList();

            if (args.Length > 0 && args[0] == Project.RESTORE_BACKUP_FILES_ARGUMENT)
            {
                project.RestoreBackupCodeFiles();
            }
            else
            {
                project.BackupCodeFiles();
                MutationEngine mutEngine = new MutationEngine(project);
                UI.ShowMessage("Beginning file mutations...");
                mutEngine.MutateFiles();
                TimeSpan elapsed = DateTime.Now - start;
                UI.ShowMessage("Execution time was " + elapsed.TotalSeconds + " seconds");
            }
        }
    }
}
