using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace CompressThis
    {
        class Program
        {
            public static string sArchiveName { get; private set; }                     // The name of the archive
            public static bool bWrite2LogFile { get; private set; }                     // Write output to external file
            public static string sLogFileLocation { get; private set; }                // The log file location
            public static string sArchiveSources { get; private set; }                // A list of the files & folders to add into the archive

            static void Main(string[] args)
            {
                try
                {
                    // Check arguments. If help is required, display, otherwise continue process.
                    if (args.Length == 1 && HelpRequired(args[0]))
                    {
                        DisplayHelp();
                    }
                    else if (args.Length < 2 || args == null)
                    {
                        // Test for arguments and throw error if not found.
                        throw new ArgumentNullException("Please define at least 2 arguments to use this utility.");
                    }
                    else
                    {
                        // Define variables
                        bool bInputWrite2Log = false;
                        string sInputLogFileLocation = string.Empty;

                        // Capture the command parameters
                        if (args.Length > 2)    // The log file parameters have been specified
                        {
                            bool boolTest = bool.TryParse(args[2], out bInputWrite2Log);

                            if (boolTest == false)
                            {
                                showMessage("Incorrect argument specified for parameter 3 (Expected: [Null]/True/False)");
                            }

                        // Check to see if the log file location is set, populate if found (adding a trailing backslash if missing)
                        if (!string.IsNullOrEmpty(args[3]))
                        {
                            sInputLogFileLocation = args[3].EndsWith(@"\") ? args[3] : args[3] + @"\";
                        }                        
                    }

                        // Write the parameters to variables for use in the application
                        sArchiveName = args[0].ToString();
                        bWrite2LogFile = bInputWrite2Log;
                        sLogFileLocation = sInputLogFileLocation;

                        // Manage the compress activities.
                        sArchiveSources = args[1].ToString();

                        // Log start of the compression
                        Log2File("Starting Compresion Utility to generate " + sArchiveName + ". Contents Reqd: " + sArchiveSources);

                        // Define variables
                        List<FileList> files2Compress = new List<FileList>();
                        List<InputList> directory2Compress = new List<InputList>();
                        List<InputList> inputList = new List<InputList>();

                        // Split the archive sources into a list
                        List<string> items = sArchiveSources.Split(',').ToList<string>();
                        string[] inputs;
                        foreach (string item in items)
                        {
                            inputs = item.Split(new char[] { '|' });
                            inputList.Add(new InputList(inputs[0].Trim()
                                                                                , inputs.ElementAtOrDefault(1) ?? inputs[0].Replace("\\" + new DirectoryInfo(inputs[0].Trim()).Name, "").Trim()
                                                                                , string.IsNullOrEmpty(inputs.ElementAtOrDefault(1)) ? false : true
                                                                                )
                                                     );

                        }

                        // for each identifed source identify whether it is a file or a directory.
                        foreach (InputList item in inputList)
                        {
                            if (File.Exists(item.sFullName))
                            {
                                // This path is a file
                                files2Compress.Add(new FileList(item.sFullName, new DirectoryInfo(item.sFullName).Name, item.sRootPath.Trim()));
                            }
                            else if (Directory.Exists(Path.GetDirectoryName(item.sFullName)))
                            {
                                // This path is a directory
                                directory2Compress.Add(new InputList(item.sFullName, item.sRootPath.Trim(), item.sFolderRootReqd));
                            }
                            else if (Directory.Exists(item.sFullName))
                            {
                                // This path is the root of a drive (mapped or UNC)
                                directory2Compress.Add(new InputList(item.sFullName, item.sRootPath.Trim(), item.sFolderRootReqd));
                            }
                            else
                            {
                                showMessage(item + " is not a valid file or directory", true);
                            }
                        }

                        // Display a message to the user & log to confirm identified number of sources.
                        showMessage(directory2Compress.Count.ToString() + " folder(s) and " + files2Compress.Count.ToString() + " file(s) identifed for compression", false);

                        // Recurse through the identified folders and add to the archive file.
                        foreach (InputList dirName in directory2Compress)
                        {
                            showMessage("Iterating though " + dirName.sFullName + " to compress files and folders", false);
                            showMessage("This may take some time dependant upon the number of files... ", false);

                            AddFolderToArchive(sArchiveName, dirName.sFullName, dirName.sFolderRootReqd);

                            showMessage("...Completed adding " + dirName.sFullName);
                        }

                        // Recurse through the identified files
                        if (files2Compress.Any())
                        {
                            int iTotalFilesCount = files2Compress.Count;
                            int iCurrentFileCount = 0;

                            string sFilePath = string.Empty;
                            string sFileName = string.Empty;

                            showMessage("Starting specified files compression", false);
                            // Loop through the files list and add to archive
                            foreach (FileList filelist in files2Compress)
                            {
                            showMessage("Adding " + filelist.sFileName);

                                if (string.IsNullOrEmpty(filelist.sRootName))
                                {
                                    AddFileToArchive(sArchiveName, filelist.sFileName, Path.GetFileName(filelist.sFileName));
                                }
                                else
                                {
                                    sFilePath = string.Empty;
                                    sFilePath = Path.GetDirectoryName(filelist.sFileName).Replace(filelist.sRootPath, "");
                                    if (string.IsNullOrEmpty(sFilePath))
                                    {
                                        sFileName = filelist.sRootName;
                                    }
                                    else
                                    {
                                        sFileName = sFilePath + "\\" + filelist.sRootName;
                                        sFileName = sFileName.StartsWith("\\") ? sFileName.Substring(1) : sFileName;
                                    }

                                    AddFileToArchive(sArchiveName, filelist.sFileName, sFileName);

                                    // Call the progress indicator
                                    iCurrentFileCount++;
                                    DisplayProgress(iTotalFilesCount, iCurrentFileCount);

                                showMessage("Added " + filelist.sFileName);

                                }
                            }
                            showMessage("....Completed", false);
                        }

                    }
                }
                catch (Exception ex)
                {
                    showMessage(ex.Message, true);
                }
            }

            #region Structures

            /// <summary>
            /// Manages the individual files for addition to archive
            /// </summary>
            private struct FileList
            {
                public string sFileName { get; private set; }       // The object name
                public string sRootName { get; private set; }        // The root name
                public string sRootPath { get; private set; }       // The root path of the object

                public FileList(string FileName, string RootName, string RootPath)
                    : this()
                {
                    this.sFileName = FileName;
                    this.sRootName = RootName;
                    this.sRootPath = RootPath;
                }
            }

            /// <summary>
            /// 
            /// </summary>
            private struct InputList
            {
                public string sFullName { get; private set; }       // The object name
                public string sRootPath { get; private set; }        // The root name
                public bool sFolderRootReqd { get; private set; }       // The root path of the object

                public InputList(string FullName, string RootPath, bool FolderRootReqd)
                    : this()
                {
                    this.sFullName = FullName;
                    this.sRootPath = RootPath;
                    this.sFolderRootReqd = FolderRootReqd;
                }
            }

            #endregion Structures

            #region "Errors and Logs"
            /// <summary>
            /// Accept a message string. If error, add prefix and write to screen. 
            /// </summary>
            /// <param name="sMsg">Error Message</param>
            /// <param name="IsError">True if error message</param>
            private static void showMessage(String sMsg, bool IsError = false)
            {
                if (IsError)
                {
                    sMsg = "Error! - Error Message: " + sMsg;
                }
                Console.WriteLine(sMsg);
                Log2File(sMsg);
            }

            /// <summary>
            /// Write message to the log file. Create file if not exists.
            /// </summary>
            /// <param name="sMsg"></param>
            private static void Log2File(String sMsg)
            {
                // Test for write to log switch.
                if (bWrite2LogFile)
                {

                // Create a new log file
                String sLogFileName = sLogFileLocation + "CompressionUtility_" + DateTime.Now.ToString("yyyyMMdd") + ".log";

                // Check if the file exists
                bool isExists = false;

                if (File.Exists(sLogFileName))
                {
                    isExists = true;
                }

                using (StreamWriter swLog =
                    new StreamWriter(sLogFileName, true))
                        {
                            // If the log file is newly created, add a header.
                            if (!isExists)
                            {
                                swLog.WriteLine("<<<----------------------------------------------------------------------->>>");
                                swLog.WriteLine("<<<------ Command Line Compression Utility (2018) ------>>>");
                                swLog.WriteLine("<<<----------------------------------------------------------------------->>>");
                                swLog.WriteLine("<<<  Log File                                                                               >>>");
                                swLog.WriteLine("<<<----------------------------------------------------------------------->>>");
                            }
                            else
                            {
                                // Write to the file
                                swLog.WriteLine(DateTime.Now);
                                swLog.WriteLine(sMsg);
                                swLog.WriteLine();
                            }
                        }
                }
            }

            #endregion "Errors and Logs"

            #region "Manage Help"
            /// <summary>
            /// Check arguments for common help symbols
            /// </summary>
            /// <param name="param"></param>
            /// <returns></returns>
            private static bool HelpRequired(string param)
            {
                return param == "-h" || param == "--help" || param == "/?";
            }

            /// <summary>
            /// Display help to the user through the console window.
            /// </summary>
            private static void DisplayHelp()
            {
                Console.WriteLine("This application uses the dotNet 4.5 framework to compress files and folders into a zip archive which can be understood by Windows and other compression utilities.");
                Console.WriteLine("The method adopted by this tool is fast, utilises low memory consumption and has a compression ratio of ~4.5:1");
                Console.WriteLine(" Input Parameters required:");
                Console.WriteLine("     #1 : The Archive name and location");
                Console.WriteLine("     #2 : The Files / Folders to be added in the format:");
                Console.WriteLine("             FolderPath | FolderPath\\FileName");
                Console.WriteLine("             Use pipe to separate the files and folders in the list. Omit the final backslash from folder paths.");
                Console.WriteLine("             All paths must be absolute.");
                Console.WriteLine("     #3 : OPTIONAL: Complete if you wish to write output to a log file. Allowed values: [Null]/True/False");
                Console.WriteLine("     #4 : OPTIONAL: Enter the full path to the log file location. If no path is specified, the log file will be writen to the same folder as the executable.");
                Console.WriteLine("");
                Console.WriteLine("USAGE:");
                Console.WriteLine("The application is called from the command line in the following method: ");
                Console.WriteLine("      CompressionUtility.exe \"Archive Path\\ArchiveName.zip\" \"Files and folders to compress\" \"[true]\" \"[LogFile location]\"");
            }

            #endregion "Manage Help"

            #region "Manage Archive"

            /// <summary>
            /// Add Files to the archive
            /// </summary>
            /// <param name="sZipPath"></param>
            /// <param name="sFileName"></param>
            /// <param name="sFilePath"></param>
            private static void AddFileToArchive(string sZipPath, string sFileName, string sFilePath)
            {
                try
                {
                    // Compress the content
                    using (FileStream zip2Open = new FileStream(sZipPath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                    {
                        using (ZipArchive archive = new ZipArchive(zip2Open, ZipArchiveMode.Update))
                        {
                            archive.CreateEntryFromFile(sFileName, sFilePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    showMessage("Unable to compress: " + sFileName + " " + ex.Message.ToString(), true);
                }
            }

            /// <summary>
            /// Add folders to the archive
            /// </summary>
            /// <param name="sZipPath"></param>
            /// <param name="sFolderPath"></param>
            /// <param name="bFolderRootReqd"></param>
            private static void AddFolderToArchive(string sZipPath, string sFolderPath, bool bFolderRootReqd)
            {
                try
                {
                    // Compress the content
                    ZipFile.CreateFromDirectory(sFolderPath, sZipPath, CompressionLevel.Optimal, bFolderRootReqd);
                }
                catch (Exception ex)
                {
                    showMessage("Unable to compress: " + sFolderPath + " " + ex.Message.ToString(), true);
                }
            }

            #endregion "Manage Archive"

            #region "Manage Progress"

            /// <summary>
            /// Displays a percentage value within the same cursor location indicating progress.
            /// </summary>
            /// <param name="iTotalCount"></param>
            /// <param name="iCurrentCount"></param>
            private static void DisplayProgress(decimal iTotalCount, decimal iCurrentCount)
            {
                decimal iPercentage = (iCurrentCount / iTotalCount) * 100;
                iPercentage = Math.Round(iPercentage, 0);
                Console.Write("\r(0)%", iPercentage);
            }

            #endregion "Manage Progress"
        }
    }