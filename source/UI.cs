using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynaMut
{
    class UI
    {
        public static void ShowMessage(string message)
        {
            Console.WriteLine(message);
        }

        public static bool GetUserConfirmation(string message)
        {
            Console.Write(message + " (y/N):");
            ConsoleKeyInfo keyInfo = Console.ReadKey(false);
            Console.WriteLine("");
            return keyInfo.KeyChar == 'y' || keyInfo.KeyChar == 'Y';
        }
    }
}
