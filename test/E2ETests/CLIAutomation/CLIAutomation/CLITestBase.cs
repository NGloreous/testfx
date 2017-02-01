﻿// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Configuration;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;

    public class CLITestBase
    {
        private static VsTestConsoleWrapper vsTestConsoleWrapper;
        private DiscoveryEventsHandler discoveryEventsHandler;
        private RunEventsHandler runEventsHandler;
        private const string vstestConsoleRelativePath = @"..\..\..\..\..\..\packages\Microsoft.TestPlatform.15.0.0-preview-20161125-02\tools\net46";
        private const string TestAssets = "TestAssets";
        private const string artifacts = "artifacts";
        private const string E2ETestsRelativePath = @"..\..\..\..\";

        public CLITestBase()
        {
            vsTestConsoleWrapper = new VsTestConsoleWrapper(this.GetConsoleRunnerPath());
            vsTestConsoleWrapper.StartSession();
        }

        /// <summary>
        /// Invokes <c>vstest.console</c> to discover tests in the provided sources.
        /// </summary>
        /// <param name="sources">Collection of test containers.</param>
        /// <param name="runSettings">Run settings for execution.</param>
        public void InvokeVsTestForDiscovery(string []sources,string runSettings = "")
        {
            for(var iterator = 0; iterator < sources.Length; iterator++)
            {
                if (!Path.IsPathRooted(sources[iterator]))
                    sources[iterator] = this.GetAssetFullPath(sources[iterator]);
            }

            discoveryEventsHandler = new DiscoveryEventsHandler();
            string runSettingXml = this.GetRunSettingXml(runSettings, GetTestAdapterPath());
            //this step of Initializing extensions should not be required after Bug 294247 is fixed
            vsTestConsoleWrapper.InitializeExtensions(Directory.GetFiles(GetTestAdapterPath(), "*TestAdapter.dll"));
            vsTestConsoleWrapper.DiscoverTests(sources, runSettingXml, discoveryEventsHandler);
        }

        /// <summary>
        /// Invokes <c>vstest.console</c> to execute tests in provided sources.
        /// </summary>
        /// <param name="sources">List of test assemblies.</param>
        /// <param name="runSettings">Run settings for execution.</param>
        public void InvokeVsTestForExecution(string []sources, string runSettings = "")
        {
            for (var iterator = 0; iterator < sources.Length; iterator++)
            {
                if (!Path.IsPathRooted(sources[iterator]))
                    sources[iterator] = this.GetAssetFullPath(sources[iterator]);
            }

            runEventsHandler = new RunEventsHandler();
            string runSettingXml = this.GetRunSettingXml(runSettings, GetTestAdapterPath());
            //this step of Initializing extensions should not be required after Bug 294247 is fixed
            vsTestConsoleWrapper.InitializeExtensions(Directory.GetFiles(GetTestAdapterPath(), "*TestAdapter.dll"));
            vsTestConsoleWrapper.RunTests(sources, runSettingXml, runEventsHandler);
        }

        /// <summary>
        /// Gets the full path to a test asset.
        /// </summary>
        /// <param name="assetName">Name of the asset with extension. E.g. <c>SimpleUnitTest.dll</c></param>
        /// <returns>Full path to the test asset.</returns>
        /// <remarks>
        /// Test assets follow several conventions:
        /// (a) They are built for provided build configuration.
        /// (b) Name of the test asset matches the parent directory name. E.g. <c>TestAssets\SimpleUnitTest\SimpleUnitTest.xproj</c> must 
        /// produce <c>TestAssets\SimpleUnitTest\bin\Debug\SimpleUnitTest.dll</c>
        /// (c) TestAssets are copied over to a central location i.e. "TestAssets\artifacts\*.*"
        /// </remarks>
        protected string GetAssetFullPath(string assetName)
        {
            var assetPath = Path.Combine(
                Environment.CurrentDirectory,
                E2ETestsRelativePath,
                TestAssets,
                artifacts,
                assetName);

            Assert.IsTrue(File.Exists(assetPath), "GetTestAsset: Path not found: {0}.", assetPath);

            return assetPath;
        }

        protected string GetTestAdapterPath()
        {
            var testAdapterPath = Path.Combine(
                Environment.CurrentDirectory,
                E2ETestsRelativePath,
                TestAssets,
                artifacts);

            return testAdapterPath;
        }

        /// <summary>
        /// Gets the RunSettingXml having testadapterpath filled in specified by arguement.
        /// Inserts testAdapterPath in existing runSetting if not present already,
        /// or generates new runSettings with testAdapterPath if runSettings is Empty.
        /// </summary>
        /// <param name="settingsXml">RunSettings provided for discovery/execution</param>
        /// <param name="testAdapterPath">Full path to TestAdapter.</param>
        /// <returns>RunSettingXml as string</returns>
        protected string GetRunSettingXml(string settingsXml, string testAdapterPath)
        {
            if(string.IsNullOrEmpty(settingsXml))
            {
                settingsXml = XmlRunSettingsUtilities.CreateDefaultRunSettings();
            }

            XmlDocument doc = new XmlDocument();
            using (var xmlReader = XmlReader.Create(new StringReader(settingsXml), new XmlReaderSettings() { XmlResolver = null, CloseInput = true }))
            {
                doc.Load(xmlReader);
            }

            XmlElement root = doc.DocumentElement;
            RunConfiguration runConfiguration = new RunConfiguration(testAdapterPath);
            XmlElement runConfigElement = runConfiguration.ToXml(); ;
            if (root[runConfiguration.SettingsName] == null)
            {
                XmlNode newNode = doc.ImportNode(runConfigElement, true);
                root.AppendChild(newNode);
            }
            else
            {
                XmlNode newNode = doc.ImportNode(runConfigElement.FirstChild, true);
                root[runConfiguration.SettingsName].AppendChild(newNode);
            }

            return doc.OuterXml;
        }

        /// <summary>
        /// Gets the path to <c>vstest.console.exe</c>.
        /// </summary>
        /// <returns>Full path to <c>vstest.console.exe</c></returns>
        public string GetConsoleRunnerPath()
        {
            var vstestConsolePath = Path.Combine(Environment.CurrentDirectory, vstestConsoleRelativePath, "vstest.console.exe");

            Assert.IsTrue(File.Exists(vstestConsolePath), "GetConsoleRunnerPath: Path not found: {0}", vstestConsolePath);

            return vstestConsolePath;
        }

        /// <summary>
        /// Validate if the discovered tests list contains provided tests.
        /// </summary>
        /// <param name="discoveredTestsList">List of tests expected to be discovered.</param>
        public void ValidateDiscoveredTests(params string[] discoveredTestsList)
        {
            foreach (var test in discoveredTestsList)
            {
                var flag = this.discoveryEventsHandler.Tests.Contains(test)
                           || this.discoveryEventsHandler.Tests.Contains(GetTestMethodName(test));
                Assert.IsTrue(flag, "Test {0} does not appear in discovered tests list.", test);
            }

            //Make sure only expected number of tests are discovered and not more.
            Assert.AreEqual(discoveredTestsList.Length, this.discoveryEventsHandler.Tests.Count);
        }

        /// <summary>
        /// Validates if the test results have the specified set of passed tests.
        /// </summary>
        /// <param name="passedTests">Set of passed tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodPass.</remarks>
        public void ValidatePassedTests(params string[] passedTests)
        {
            //Make sure only expected number of tests passed and not more.
            Assert.AreEqual(passedTests.Length, this.runEventsHandler.PassedTests.Count);

            foreach (var test in passedTests)
            {
                var testFound = this.runEventsHandler.PassedTests.Where(p => p.TestCase.FullyQualifiedName.Equals(test) ||
                           p.TestCase.FullyQualifiedName.Equals(GetTestMethodName(test)));
                Assert.IsNotNull(testFound, "Test {0} does not appear in passed tests list.", test);
            }
        }

        /// <summary>
        /// Validates if the test results have the specified set of failed tests.
        /// </summary>
        /// <param name="failedTests">Set of failed tests.</param>
        /// <remarks>
        /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
        /// Also validates whether these tests have stack trace info.
        /// </remarks>
        public void ValidateFailedTests(string source,params string[] failedTests)
        {
            //Make sure only expected number of tests failed and not more.
            Assert.AreEqual(failedTests.Length, this.runEventsHandler.FailedTests.Count);

            foreach (var test in failedTests)
            {
                var testFound = this.runEventsHandler.FailedTests.Where(f => f.TestCase.FullyQualifiedName.Equals(test) ||
                           f.TestCase.FullyQualifiedName.Equals(GetTestMethodName(test)));
                Assert.IsNotNull(testFound, "Test {0} does not appear in failed tests list.", test);

                //Skipping this check for x64 as of now. Bug #299488 should fix this.
                if (source.IndexOf("x64") == -1)
                {
                    // Verify stack information as well.               
                    Assert.IsTrue(testFound.First().ErrorStackTrace.Contains(GetTestMethodName(test)), "No stack trace for failed test: {0}", test);
                }
            }
        }

        /// <summary>
        /// Validates if the test results have the specified set of skipped tests.
        /// </summary>
        /// <param name="skippedTests">The set of skipped tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
        public void ValidateSkippedTests(params string[] skippedTests)
        {
            //Make sure only expected number of tests skipped and not more.
            Assert.AreEqual(skippedTests.Length, this.runEventsHandler.SkippedTests.Count);

            foreach (var test in skippedTests)
            {
                var testFound = this.runEventsHandler.SkippedTests.Where(s => s.TestCase.FullyQualifiedName.Equals(test) ||
                           s.TestCase.FullyQualifiedName.Equals(GetTestMethodName(test)));
                Assert.IsNotNull(testFound, "Test {0} does not appear in skipped tests list.", test);
            }
        }

        /// <summary>
        /// Gets the test method name from full name.
        /// </summary>
        /// <param name="testFullName">Fully qualified name of the test.</param>
        /// <returns>Simple name of the test.</returns>
        private static string GetTestMethodName(string testFullName)
        {
            string testMethodName = string.Empty;

            var splits = testFullName.Split('.');
            if (splits.Count() >= 3)
            {
                testMethodName = splits[2];
            }

            return testMethodName;
        }
    }
}
