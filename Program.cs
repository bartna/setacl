using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

//icacls "c:\test" /reset /T /C /Q

namespace setacl
{
    class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello World!");
            string mydir= @"c:\test";
            //DirectoryInfo dir = new DirectoryInfo(dir);
            DateTime dtstart = DateTime.Now;
            foreach (string myfile in Directory.EnumerateFileSystemEntries(mydir))
            {
                Program.RemoveInheritance(myfile, true);
                //Program.RestoreInheritanceAndDeleteAllOtherRules(myfile);
            }            
            DateTime dtend = DateTime.Now;
            TimeSpan ts = dtend.Subtract(dtstart);
            Console.WriteLine(ts.TotalSeconds.ToString() );
        }


        public static void RemoveInheritance(string filePath, bool copyPermisions)
        {

            DirectoryInfo x = new DirectoryInfo(filePath);

            DirectorySecurity s = x.GetAccessControl(AccessControlSections.Owner |
                AccessControlSections.Group |
                AccessControlSections.Access);

            s.SetAccessRuleProtection(true, copyPermisions);
            
            x.SetAccessControl(s);
        }

        public static void RestoreInheritanceAndDeleteAllOtherRules(string filePath)
        {

            DirectoryInfo x = new DirectoryInfo(filePath);

            DirectorySecurity s = x.GetAccessControl(AccessControlSections.None);

            foreach (AuthorizationRule ar in s.GetAccessRules(true, true, typeof(NTAccount) ) )
            {
                FileSystemAccessRule fileRule = ar as FileSystemAccessRule;
                s.RemoveAccessRule(fileRule);
            }

            s.SetAccessRuleProtection(false, false);

            x.SetAccessControl(s);
        }

    }
}
