using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.Hdfs;

namespace Microsoft.WinHdfsManagedTest
{
    class Program
    {
        private const string BaseHdfsPath = "/user/isotope/winlib";

        static void Main(string[] args)
        {
            Action<string> exitEnvironment = (message) =>
            {
                Console.WriteLine(message);
                Console.WriteLine("Hit Enter to Finish...");
                Console.ReadLine();
                Environment.Exit(0);
            };

            try
            {
                using (HdfsFileSystem hdfsSystem = HdfsFileSystem.Connect("127.0.0.1", 9000))
                {
                    Console.WriteLine("TESTING HdfsFileStream:");
                    FileTestStream(hdfsSystem);

                    Console.WriteLine("TESTING HdfsFileHandle:");
                    FileTestHandle(hdfsSystem, exitEnvironment);
                
                    Console.WriteLine("TESTING HdfsFileSystem:");
                    FileTestSystem(hdfsSystem);
                }
            }
            catch (IOException ex)
            {
                exitEnvironment(string.Format("Error '{0}' operating on the file...", ex.Message));
            }

            Console.WriteLine();
            Console.WriteLine("Hit Enter to Finish...");
            Console.ReadLine();
        }

        private static void FileTestSystem(HdfsFileSystem hdfsSystem)
        {
            string localfilename = "MobileSampleData.txt";
            string localfilepath = Path.GetFullPath(@"..\..\..\Sample") + @"\" + localfilename;

            string filename = BaseHdfsPath + "/testdata.txt";

            if (File.Exists(localfilepath))
            {
                string localhdfsfilename = BaseHdfsPath + @"\" + localfilename;

                // Show blocks used by file
                List<string> hosts = hdfsSystem.GetHosts(localhdfsfilename, 0, 1024);
                if (hosts != null)
                {
                    Console.WriteLine("File Hosts:");
                    foreach (string host in hosts)
                    {
                        Console.WriteLine("\t" + host);
                    }
                }
            }

            // Duplicate the file and modify some properties
            string subhdfspath = BaseHdfsPath + "/duplicate";
            string subfilename = subhdfspath + "/duplicatedata.txt";

            Console.WriteLine();
            Console.WriteLine("Creating file duplicate: " + subfilename);
            hdfsSystem.CreateDirectory(subhdfspath);
            HdfsFileSystem.Copy(hdfsSystem, filename, hdfsSystem, subhdfspath);
            hdfsSystem.SetTimes(filename, DateTime.Now.AddDays(-2), DateTime.Now.AddDays(-1));

            // Perform a directory parse and display
            Console.WriteLine();
            hdfsSystem.SetWorkingDirectory(BaseHdfsPath);
            using (HdfsFileInfoEntry pathinfo = hdfsSystem.GetPathInfo(BaseHdfsPath))
            {
                if (pathinfo != null)
                {
                    string kind = pathinfo.Kind == HdfsFileInfoEntryKind.Directory ? "Directory" : "\tFile";
                    Console.WriteLine(string.Format(@"{0}:""{1}"", Modified/Accessed:""{2:G}, {3:G}"", Owner:""{4}""", kind, pathinfo.Name, pathinfo.LastModified, pathinfo.LastAccessed, pathinfo.Owner));
                }
            }

            Action<string> processDirectory = null;
            processDirectory = (looppath) =>
            {
                using (HdfsFileInfoEntries entries = hdfsSystem.ListDirectory(looppath))
                {
                    foreach (HdfsFileInfoEntry entry in entries.Entries)
                    {
                        string kind = entry.Kind == HdfsFileInfoEntryKind.Directory ? "Directory" : "\tFile";
                        Console.WriteLine(string.Format(@"{0}:""{1}"", Modified/Accessed:""{2:G}, {3:G}"", Owner:""{4}""", kind, entry.Name, entry.LastModified, entry.LastAccessed, entry.Owner));
                        if (entry.Kind == HdfsFileInfoEntryKind.Directory)
                        {
                            processDirectory(entry.Name);
                        }
                    }
                }
            };
            processDirectory(BaseHdfsPath);

            Console.WriteLine();
        }

        ///  the FileHandling
        
        private static void FileTestHandle(HdfsFileSystem hdfsSystem, Action<string> exitEnvironment)
        {
            Action<int, HdfsFileHandle> checkOperation = (bytecount, file) =>
            {
                if (bytecount == -1)
                {
                    try
                    {
                        file.Close();
                    }
                    finally
                    {
                        exitEnvironment("Error operating on the file...");
                    }
                }
            };

            string localfilename = "MobileSampleData.txt";
            string localfilepath = Path.GetFullPath(@"..\..\..\Sample") + @"\" + localfilename;
            string localfilewrite = Path.GetFullPath(@"..\..\..\Sample") + @"\Hdfs." + localfilename;

            string filename = BaseHdfsPath + "/testdata.txt";

            int chunksize = 1024;
            int chunk;

            // First write out some data
            Console.WriteLine("Working Directory is: " + hdfsSystem.GetWorkingDirectory());
            Console.WriteLine("Creating New File: " + filename);
            string data = "I am some unstructured data.\nThat will be written.\n";
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            int dataLen = dataBytes.Length;

            using (HdfsFileHandle file = hdfsSystem.OpenFileForWrite(filename, chunksize, 0, 0))
            {
                checkOperation(file.WriteBytes(dataBytes, 0, data.Length), file);

                checkOperation(file.WriteByte((byte)9), file);
                checkOperation(file.WriteLine("1) This is an inserted line."), file);
                checkOperation(file.WriteByte((byte)9), file);
                checkOperation(file.WriteLine("2) This is an inserted line."), file);

                file.Flush();
            }

            // Ensure you can read the data
            using (HdfsFileHandle file = hdfsSystem.OpenFileForRead(filename))
            {
                Console.WriteLine("Data Written:");

                byte[] newDataBytes = new byte[dataLen];
                checkOperation(file.ReadBytes(newDataBytes, 0, newDataBytes.Length), file);
                Console.Write(Encoding.UTF8.GetString(newDataBytes));
                Console.Write((char)file.ReadByte());
                Console.WriteLine(file.ReadLine());
                Console.WriteLine(file.ReadLine());
            }

            // Read the full contents of a file using lines
            Console.WriteLine();
            Console.WriteLine("Reading all Line Data:");
            using (HdfsFileHandle file = hdfsSystem.OpenFileForRead(filename))
            {
                String line;
                while ((line = file.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                }
            }

            if (File.Exists(localfilepath))
            {
                // Copy a file from the local file system into Hadoop
                Console.WriteLine();
                Console.WriteLine("Copying Local File: " + localfilename);
                byte[] localbytes = new byte[chunksize];

                string localhdfsfilename = BaseHdfsPath + @"\" + localfilename;
                using (HdfsFileHandle file = hdfsSystem.OpenFileForWrite(localhdfsfilename, chunksize, 0, 0))
                {
                    using (FileStream stream = new FileStream(localfilepath, FileMode.Open, FileAccess.Read))
                    {
                        while ((chunk = stream.Read(localbytes, 0, chunksize)) > 0)
                        {
                            checkOperation(file.WriteBytes(localbytes, 0, chunk), file);
                        }
                    }

                    file.Flush();
                }

                // Copy the HDFS file to the local system
                Console.WriteLine();
                Console.WriteLine("Copying file to local:");

                using (HdfsFileHandle file = hdfsSystem.OpenFileForRead(localhdfsfilename, chunksize))
                {
                    using (FileStream stream = new FileStream(localfilewrite, FileMode.Create, FileAccess.Write))
                    {
                        while ((chunk = file.ReadBytes(readBytes, 0, chunksize)) > 0)
                        {
                            stream.Write(readBytes, 0, chunk);
                        }
                    }
                }

                Console.WriteLine(String.Format("File '{0}' Created.", localfilewrite));
                Console.WriteLine();
            }
        }
    }
}
