using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

//icacls "c:\test" /reset /T /C /Q
// 9s : 2s@20/500 :D

namespace setacl
{
    class Program 
    {
        //default input parameters
        public static int th_cnt = 20;
        public static int item_per_th_cnt = 3000;
        public static string myFirstDir = @"";
        public static string logFilePath = @"";
        public static bool writeToLog = false;
        public static bool writeToConsole = true;
        public static string logDateFormat = "yyyy-MM-dd HH:mm:ss.fff";
        public static LogLevels LogLevel = LogLevels.INFO;
        
        //logging vars
        public enum LogLevels { ALL=5, TRACE=10, DEBUG=15, INFO=20, WARN=25, ERORR=30, FATAL=35, OFF=40 }
        public static StringBuilder sb_log = new StringBuilder();
                    
        public static class print
        {
            public static readonly object sem_consolePrint = new object();
            public static void all(string txt)
            {
                if (LogLevels.ALL >= LogLevel) _print(txt, LogLevels.ALL);
            }
            public static void trace(string txt)
            {
                if (LogLevels.TRACE >= LogLevel) _print(txt, LogLevels.TRACE);
            }
            public static void debug(string txt)
            {
                if (LogLevels.DEBUG >= LogLevel) _print(txt, LogLevels.DEBUG);
            }
            public static void info(string txt)
            {
                if (LogLevels.INFO >= LogLevel) _print(txt, LogLevels.INFO);
            }
            public static void warn(string txt)
            {
                if (LogLevels.WARN >= LogLevel) _print(txt, LogLevels.WARN);
            }
            public static void error(string txt)
            {
                if (LogLevels.ERORR>= LogLevel) _print(txt, LogLevels.ERORR);
            }
            public static void FATAL(string txt)
            {
                if (LogLevels.FATAL >= LogLevel) _print(txt, LogLevels.FATAL);
                
                //always write FATAL to console
                if(!writeToConsole)
                {
                    lock (sem_consolePrint)
                    {
                        string msg = string.Format("{0} [FATAL]: {1}"
                            , new string[] { DateTime.Now.ToString(logDateFormat) , txt } );
                        Console.WriteLine(msg);
                    }
                }
            }
            private static void _print(string txt, LogLevels lvl)
            {
                lock (sem_consolePrint)
                {
                    string msg = DateTime.Now.ToString(logDateFormat) + txt;
                    msg = string.Format("{0} [{1}]: {2}"
                           , new string[] { DateTime.Now.ToString(logDateFormat), lvl.ToString(),  txt });
                    if (writeToConsole) Console.WriteLine(msg);
                    if (writeToLog) sb_log.Append(Environment.NewLine + msg);
                }
            }

        }

        static void PrintInfo()
        {
            Console.WriteLine();
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine("   SETACL   ::   Reset ACL folder permissions using multithreading");
            Console.WriteLine("                 Check App.config file for more MT settings");
            Console.WriteLine("-------------------------------------------------------------------------------");
            Console.WriteLine();
        }
        static void PrintUsage()
        {
            Console.WriteLine(string.Format("        Usage :: {0} {{root directory}}", new string[] { Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) }));
            Console.WriteLine();
            Console.WriteLine(@"   {root directory} :: path to reset (e.g. c:\test)");
            Console.WriteLine();
        }

        static void Main(string[] args)
        {
            //acl_demo();
            //return;
            PrintInfo();

            if (args.Length < 1 || string.IsNullOrWhiteSpace( args[0] ) )
            {
                PrintUsage();
                return;
            }

            myFirstDir = args[0];
            Console.WriteLine("   {root directory} :: '" + myFirstDir +"'");
            Console.WriteLine();

            LoadInputParameters();
            print.trace("Logging started");
            print.info("Parameters loaded from App.config:");
            print.info("ThreadCount=" + th_cnt.ToString());
            print.info("HowManyItemsPerThread=" + item_per_th_cnt.ToString());
            print.info("LogFilePath=" + logFilePath.ToString());
            print.info("WriteLogToFile=" + writeToLog.ToString());
            print.info("WriteLogToConsole=" + writeToConsole.ToString());
            print.info("LogDateFormat=" + logDateFormat.ToString());
            print.info("LogLevel=" + LogLevel.ToString());

            if (!Directory.Exists(myFirstDir))
            {
                print.FATAL("Directory '" + myFirstDir + "' does not exists! (EXIT)");
                return;
            }

            if(writeToLog && ( string.IsNullOrWhiteSpace(logFilePath) || !Directory.Exists( Path.GetDirectoryName(logFilePath) ) ) )
            {
                print.FATAL("Write to log file is enabled but directory for log file does not exists! '" + logFilePath + "' (EXIT)");
                return;
            }

            //icacls "c:\test" /reset /T /C /Q

            DateTime dtstart = DateTime.Now;
            int citems = 0;
            int cdirs = 0;
            int cfiles = 0;
            int citemschgd = 0;
            Queue<string> filesToTravers = new Queue<string>();

            //setup threading vars
            ParameterizedThreadStart tsp_oneParamQueueString = new ParameterizedThreadStart(th_execQueue);
            Thread[] my_threads = new Thread[th_cnt];

            //create a queue for traversing through dirs and insert the root directory
            Queue<string> dirsToTravers = new Queue<string>();
            dirsToTravers.Enqueue(myFirstDir);

            //create a queues for file paths one per each thread
            Queue<string>[] fileQueues = new Queue<string>[th_cnt];
            for (int i = 0; i < th_cnt; i++) fileQueues[i] = new Queue<string>();

            //main loop for traversing through dirs & files
            int current_thread = 0;
            for (; dirsToTravers.Count > 0; cdirs++)
            {
                string curfolder = dirsToTravers.Dequeue();
                fileQueues[current_thread].Enqueue(curfolder);

                //every new folder should be added to the directory queue
                foreach (string myDir in Directory.EnumerateDirectories(curfolder))
                {
                    print.debug("dir : " + myDir);
                    dirsToTravers.Enqueue(myDir);
                }
                
                //every new file should be handled
                foreach (string myfile in Directory.EnumerateFiles(curfolder))
                {
                    //continue;
                    print.debug("file: " + myfile);
                    cfiles++;
                    fileQueues[current_thread].Enqueue(myfile);
                    //when current file queue is full create a thread to do the job
                    if (fileQueues[current_thread].Count >= item_per_th_cnt)
                    {                        
                        my_threads[current_thread] = new Thread(new ParameterizedThreadStart(tsp_oneParamQueueString));
                        my_threads[current_thread].Start(fileQueues[current_thread]);

                        //move to the next thread
                        current_thread = (current_thread + 1) % th_cnt;

                        //wait for the thread to complete before continuing to make sure that the Queue is already empty 
                        //[this aproach might be futherly optimize]
                        if (my_threads[current_thread] != null) my_threads[current_thread].Join();
                    }

                }
            }

            if( fileQueues[current_thread].Count > 0 )
            {
                //last batch of files. Thread {current_thread} is already joined in the above loop
                my_threads[current_thread] = new Thread(new ParameterizedThreadStart(tsp_oneParamQueueString));
                my_threads[current_thread].Start(fileQueues[current_thread]);
            }

            print.debug("//need to wait for the all threads to finish");
            for (int i = 0; i < th_cnt; i++)
            {
                print.debug("//trying to close thread " + i.ToString());
                if (my_threads[i] != null) my_threads[i].Join();
                print.debug("//thread " + i.ToString() + " is closed");
            }


            citems = cfiles + cdirs;
            
            //disabled single-threaded single-folder version
            if (false)
            {
                foreach (string myfile in Directory.EnumerateFileSystemEntries(myFirstDir))
                {
                    filesToTravers.Enqueue(myfile);
                    citems++;
                }

                for (; filesToTravers.Count > 0;)
                {
                    string myfile = filesToTravers.Dequeue();
                    //j += Program.RemoveInheritance(myfile, true) ? 1 : 0;
                    //Program.RemoveInheritance(myfile, true);
                    Program.RestoreInheritanceAndDeleteAllOtherRules(myfile);
                }
            }

            //calculate time of the run
            DateTime dtend = DateTime.Now;
            TimeSpan ts = dtend.Subtract(dtstart);

            //print some statistics
            print.info(string.Format("Work finished in : {0:n4} s", ts.TotalSeconds ) );
            print.info("Number of items: "+ citems.ToString() + ", files: "+ cfiles + ", dirs: " + cdirs ) ;
            print.info(string.Format("Successfully processed {0} files; Failed processing {1} files", th_itemscnt , th_itemscnt_failed));



            if ( writeToLog ) System.IO.File.WriteAllText(logFilePath, sb_log.ToString());

        }

        private static void LoadInputParameters()
        {
            string sPar;
            int iPar;
            bool bPar;
            object lPar;


            if (int.TryParse(ConfigurationManager.AppSettings.Get("ThreadCount"), out iPar) ) th_cnt = iPar;
            if (int.TryParse(ConfigurationManager.AppSettings.Get("HowManyItemsPerThread"), out iPar) ) item_per_th_cnt = iPar;

            if (bool.TryParse(ConfigurationManager.AppSettings.Get("WriteLogToFile"), out bPar) ) writeToLog = bPar;
            if (bool.TryParse(ConfigurationManager.AppSettings.Get("WriteLogToConsole"), out bPar) ) writeToConsole = bPar;

            sPar = ConfigurationManager.AppSettings.Get("LogFilePath");
            if (!string.IsNullOrWhiteSpace(sPar)) logFilePath = sPar;

            sPar = ConfigurationManager.AppSettings.Get("LogDateFormat");
            if (!string.IsNullOrWhiteSpace(sPar)) logDateFormat = sPar;

            sPar = ConfigurationManager.AppSettings.Get("LogLevel");

            if (Enum.TryParse( typeof(LogLevels), ConfigurationManager.AppSettings.Get("LoggingLevel"), out lPar)) LogLevel = (LogLevels)lPar;
    }

    private static void acl_demo()
        {
            DirectoryInfo x = new DirectoryInfo(@"e:\test\a.txt");

            DirectorySecurity s = x.GetAccessControl();

            foreach (AuthorizationRule ar in s.GetAccessRules(true, true, typeof(NTAccount)))
            {
                FileSystemAccessRule fileRule = ar as FileSystemAccessRule;
                s.RemoveAccessRule(fileRule);
            }

            s.SetAccessRuleProtection(false, false);

            //x.SetAccessControl(s);
        }

        public static bool checkIfInheritanceIsSet = false; //false= 5s:5s; true=9s:2s

        public static bool RemoveInheritance(string filePath, bool copyPermisions)
        {
            //return true;
            DirectoryInfo x = new DirectoryInfo(filePath);

            DirectorySecurity s = x.GetAccessControl(AccessControlSections.Owner |
                AccessControlSections.Group |
                AccessControlSections.Access );

            if (!checkIfInheritanceIsSet)
            {
                s.SetAccessRuleProtection(true, copyPermisions);
                x.SetAccessControl(s);
                return true;
            }

            //only if checkIfInheritanceIsSet=true; SLOWER
            if (s.GetAccessRules(false, true, typeof(NTAccount)).Count > 0 )
            {
                s.SetAccessRuleProtection(true, copyPermisions);
                x.SetAccessControl(s);
                return true;
            }            

            return false;
        }

        public static void RestoreInheritanceAndDeleteAllOtherRules(string filePath)
        {

            DirectoryInfo di = new DirectoryInfo(filePath);

            DirectorySecurity ds = di.GetAccessControl();

            foreach (AuthorizationRule ar in ds.GetAccessRules(true, false, typeof(NTAccount) ) )
            {
                FileSystemAccessRule fileRule = ar as FileSystemAccessRule;
                ds.RemoveAccessRule(fileRule);
            }

            ds.SetAccessRuleProtection(false, false);

            di.SetAccessControl(ds);
        }

        public static int th_itemscnt = 0;
        public static int th_itemscnt_failed = 0;
        public static readonly object th_itemscount_lock = new object();
        public static void th_execQueue(object p_queue_obj)
        {
            string threadInfo = "Thread(" + System.Threading.Thread.CurrentThread.ManagedThreadId + "):";
            try
            {
                print.debug(threadInfo + " is working on CPU " + System.Threading.Thread.GetCurrentProcessorId());
                if (p_queue_obj is Queue<string>)
                {
                    Queue<string> p_queue = (Queue<string>)p_queue_obj;
                    print.debug(threadInfo + " got " + p_queue.Count + " files");
                    for (; p_queue.Count > 0;)
                    {
                        string file = p_queue.Dequeue();
                        print.debug(threadInfo + " handling file: " + file);

                        //perform the task
                        try
                        {
                            RestoreInheritanceAndDeleteAllOtherRules(file);
                            //count all items that have been updated by the threads
                            lock (th_itemscount_lock)
                            {
                                th_itemscnt++;
                            }
                        }
                        catch (Exception ee)
                        {
                            //count all items that have NOT been updated by the threads
                            print.error("Entry not changed due to the exception: " + ee.ToString() + "; Unchaned file: " + file);
                            lock (th_itemscount_lock)
                            {
                                th_itemscnt_failed++;
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("Wrong argument for the function th_execQueue!");
                }
                print.debug(threadInfo + " exiting thread");
            }
            catch(Exception ee)
            {
                print.error("threadInfo: " + ee.Message);
            }
        }

    }
}
