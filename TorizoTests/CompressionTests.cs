using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Torizo.Tests
{
    [TestClass()]
    public class CompressionTests
    {
        const string TestDataDir = @"TestData";

        [TestMethod()]
        public void CompressDecompressIsDeterministic()
        {
            var testDataContents = Directory.EnumerateFiles(TestDataDir);

            foreach (string file in testDataContents)
            {
                byte[] fileData = File.ReadAllBytes(file);

                byte[] compressedData = Compression.CompressData(fileData);
                byte[] decompressedData = Compression.DecompressData(compressedData);

                Assert.AreEqual(fileData.Length, decompressedData.Length, $"Data length differs. Should be {fileData.Length} bytes long but was actually {decompressedData.Length} bytes long.");

                for (int i = 0; i < Math.Min(fileData.Length, decompressedData.Length); ++i)
                    Assert.AreEqual(fileData[i], decompressedData[i], $"Data differs at position {i}. Expected <{fileData[i]}> but got <{decompressedData[i]}>.");
            }
        }
    }
}