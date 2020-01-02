using Microsoft.VisualStudio.TestTools.UnitTesting;
using Torizo.Lunar;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Torizo.Lunar.Tests
{
    [TestClass()]
    public class LunarCompressionTests
    {
        const string TestDataDir = @"TestData";

        [TestMethod()]
        public void CompressDecompressIsDeterministic()
        {
            var testDataContents = Directory.EnumerateFiles(TestDataDir);

            foreach (string file in testDataContents)
            {
                byte[] fileData = File.ReadAllBytes(file);

                byte[] compressedData = LunarCompression.RecompressNew(fileData);
                byte[] decompressedData = LunarCompression.DecompressNew(compressedData);

                Assert.AreEqual(fileData.Length, decompressedData.Length, $"Data length differs. Should be {fileData.Length} bytes long but was actually {decompressedData.Length} bytes long.");

                for (int i = 0; i < Math.Min(fileData.Length, decompressedData.Length); ++i)
                    Assert.AreEqual(fileData[i], decompressedData[i], $"Data differs at position {i}. Expected <{fileData[i]}> but got <{decompressedData[i]}>.");
            }
        }
    }
}