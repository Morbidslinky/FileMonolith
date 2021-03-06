﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using GzsTool.Core.Gzs;
using GzsTool.Core.Qar;
using GzsTool.Core.Common.Interfaces;
using GzsTool.Core.Common;
using GzsTool.Core.Utility;
using System.Xml.Serialization;
using System.Threading;
using System.Collections;

namespace ArchiveTransferrer
{
    class TransferManager
    {
        public event EventHandler<FeedbackEventArgs> SendFeedback;
        public ArrayList successfulTransfers = new ArrayList();
        public string errorOccurred = "";

        protected virtual void OnSendFeedback(object feedback)
        {
            SendFeedback?.Invoke(this, new FeedbackEventArgs() { Feedback = feedback });
        }

        public void Transfer(string GZg0sSrc, string MGODatSrc, string masterDir)
        {
            string workDir = "temp";

            try
            {
                OnSendFeedback("Reading dictionaries...");
                // READ DICTIONARIES
                ReadDictionaries();


                OnSendFeedback("Creating temp directory...");
                // CREATE WORK DIRECTORY
                string g0sWorkDir = Path.Combine(workDir, "texture6_gzs0_dat");
                Directory.CreateDirectory(g0sWorkDir);


                OnSendFeedback("Copying Ground Zeroes Archive...");
                // COPY G0S TO WORK DIRECTORY
                string GZg0sDst = Path.Combine(workDir, Path.GetFileName(GZg0sSrc));
                File.Copy(GZg0sSrc, GZg0sDst, true);


                OnSendFeedback("Extracting data_01.g0s...");
                // OPEN G0S
                IDirectory outputDirectory = new FileSystemDirectory(g0sWorkDir);
                using (FileStream input = new FileStream(GZg0sDst, FileMode.Open))
                {
                    GzsFile file = new GzsFile();
                    file.Name = Path.GetFileNameWithoutExtension(GZg0sDst);
                    file.Read(input);
                    foreach (var exportedFile in file.ExportFiles(input))
                    {
                        OnSendFeedback(exportedFile.FileName);
                        outputDirectory.WriteFile(exportedFile.FileName, exportedFile.DataStream);
                    }
                }


                OnSendFeedback("Copying texture6_gzs0.dat.xml...");
                // COPY REFERENCE XML TO WORK DIRECTORY
                string datXmlDstPath = Path.Combine(workDir, "texture6_gzs0.dat.xml");
                File.Copy("texture6_gzs0.dat.xml", datXmlDstPath, true);

                OnSendFeedback("Formatting texture6_gzs0.dat...");
                // WRITE DAT
                WriteArchive(datXmlDstPath);


                OnSendFeedback("Moving texture6_gzs0.dat...");
                // COPY DAT TO MASTER/
                string GZDatSrc = Path.Combine(workDir, "texture6_gzs0.dat");
                string GZDatDst = Path.Combine(masterDir, "texture6_gzs0.dat");
                File.Copy(GZDatSrc, GZDatDst, true);
                successfulTransfers.Add("texture6_gzs0.dat");


                OnSendFeedback("Copying MGO Archive...");
                // COPY MGO DAT TO MASTER/
                string MGODatDst = Path.Combine(masterDir, "texture5_mgo0.dat");
                File.Copy(MGODatSrc, MGODatDst, true);
                successfulTransfers.Add("texture5_mgo0.dat");
            }
            catch (Exception e)
            {
                errorOccurred = e.Message;
            }
            finally
            {
                DeleteDirectory(workDir);
            }
        }

        public string GetSuccessfulTransfers()
        {
            string transferList = "";
            foreach (string transfer in successfulTransfers)
            {
                transferList += "\n" + transfer;
            }
            return transferList;
        }

        private static void ReadDictionaries()
        {
            string executingAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            /*
            const string qarDictionaryName = "qar_dictionary.txt";
            try
            {
                Hashing.ReadDictionary(Path.Combine(executingAssemblyLocation, qarDictionaryName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", qarDictionaryName, e.Message);
            }
            */
            const string gzsDictionaryName = "gzs_dictionary.txt";
            try
            {
                Hashing.ReadDictionaryLegacy(Path.Combine(executingAssemblyLocation, gzsDictionaryName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", gzsDictionaryName, e.Message);
            }
            /*
            const string fpkDictionaryName = "fpk_dictionary.txt";
            try
            {
                Hashing.ReadMd5Dictionary(Path.Combine(executingAssemblyLocation, fpkDictionaryName));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading {0}: {1}", fpkDictionaryName, e.Message);
            }
            */
        }

        private static readonly XmlSerializer ArchiveSerializer = new XmlSerializer(
            typeof(ArchiveFile),
            new[] { typeof(QarFile) });

        private static void WriteArchive(string path)
        {
            var directory = Path.GetDirectoryName(path);
            using (FileStream xmlInput = new FileStream(path, FileMode.Open))
            {
                ArchiveFile file = ArchiveSerializer.Deserialize(xmlInput) as ArchiveFile;
                if (file == null)
                {
                    Console.WriteLine("Error: Unknown archive type");
                    return;
                }

                WriteArchive(file, directory);
            }
        }

        private static void WriteArchive(ArchiveFile archiveFile, string workingDirectory)
        {
            string outputPath = Path.Combine(workingDirectory, archiveFile.Name);
            string fileSystemInputDirectory = string.Format("{0}\\{1}_{2}", workingDirectory,
                Path.GetFileNameWithoutExtension(archiveFile.Name), Path.GetExtension(archiveFile.Name).Replace(".", ""));
            IDirectory inputDirectory = new FileSystemDirectory(fileSystemInputDirectory);
            using (FileStream output = new FileStream(outputPath, FileMode.Create))
            {
                archiveFile.Write(output, inputDirectory);
            }
        }

        public void DeleteDirectory(string target_dir)
        {

            OnSendFeedback("Cleaning up " + Path.GetFileName(target_dir));
            foreach (string file in Directory.EnumerateFiles(target_dir))
            {
                //Debug.LogLine("[Cleanup Debug] Setting FileAttributes for " + file);
                File.SetAttributes(file, FileAttributes.Normal);
                //Debug.LogLine("[Cleanup Debug] Deleting " + file);
                File.Delete(file);
            }
            foreach (string dir in Directory.EnumerateDirectories(target_dir))
            {
                //Debug.LogLine("[Cleanup Debug] Deleting " + dir);
                DeleteDirectory(dir);
            }

            //Debug.LogLine("[Cleanup Debug] Deleting " + target_dir);
            DirectoryInfo target = new DirectoryInfo(target_dir);
            if (target.GetFiles().Length == 0)
                Directory.Delete(target_dir, true);
            else
            {
                Thread.Sleep(50);
                DeleteDirectory(target_dir);
            }
        }
    }
}
