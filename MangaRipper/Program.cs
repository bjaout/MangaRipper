﻿using System;
using System.IO;
using System.Windows.Forms;
using MangaRipper.Forms;
using NLog;
using SimpleInjector;
using MangaRipper.Core.Interfaces;
using System.Linq;
using System.Reflection;
using MangaRipper.Core.Models;
using MangaRipper.Core;
using MangaRipper.Infrastructure;
using MangaRipper.Core.Outputers;
using MangaRipper.Core.Renaming;
using MangaRipper.Core.FilenameDetectors;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using OpenQA.Selenium.Remote;

namespace MangaRipper
{
    internal static class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static Container container;
        static ChromeDriver driver;
        static Timer splashTimer;
        static SplashScreen splash;
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Logger.Info("> Main()");
            var appDomain = AppDomain.CurrentDomain;
            appDomain.UnhandledException += AppDomain_UnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            splashTimer = new Timer
            {
                Interval = 500
            };
            splashTimer.Tick += SplashTimer_Tick;
            splashTimer.Start();
            splash = new SplashScreen();
            splash.ShowDialog();
            Application.Run(container.GetInstance<FormMain>());

            driver.Close();
            driver.Quit();
            Logger.Info("< Main()");
        }

        private static void SplashTimer_Tick(object sender, EventArgs e)
        {
            if(driver == null && splash.Visible)
            {
                splashTimer.Stop();
                Bootstrap();
                splash.Close();
            }
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            Logger.Fatal(ex, "Unhandled Exception");
            driver.Close();
            driver.Quit();
        }

        private static void Bootstrap()
        {
            InitChromeDriver();

            container = new Container();

            container.RegisterConditional(typeof(Core.Interfaces.ILogger),
               c => typeof(NLogLogger<>).MakeGenericType(c.Consumer.ImplementationType),
               Lifestyle.Transient,
               c => true
               );

            container.Register<RemoteWebDriver>(() => driver);

            container.Register<IOutputFactory, OutputFactory>();

            var configPath = Path.Combine(Environment.CurrentDirectory, "MangaRipper.Configuration.json");
            container.Register<IConfiguration>(() => new Configuration(configPath));
            container.Register<IDownloader, Downloader>();
            container.Register<IXPathSelector, HtmlAtilityPackAdapter>();
            container.Register<IRetry, Retry>();

            container.Register<IFilenameDetector, FilenameDetector>();
            container.Register<IGoogleProxyFilenameDetector, GoogleProxyFilenameDetector>();


            container.Register<IFileManipulation, FileManiuplation>();

            var pluginPath = Path.Combine(Environment.CurrentDirectory, "Plugins");
            var pluginAssemblies = new DirectoryInfo(pluginPath).GetFiles()
                .Where(file => file.Extension.ToLower() == ".dll" && file.Name.StartsWith("MangaRipper.Plugin."))
                .Select(file => Assembly.Load(AssemblyName.GetAssemblyName(file.FullName)));

            container.Collection.Register<IMangaService>(pluginAssemblies);

            container.RegisterDecorator<IXPathSelector, XPathSelectorLogging>();
            container.RegisterDecorator<IDownloader, DownloadLogging>();
            container.Register<FormMain>();
            //container.Verify();
        }

        private static void InitChromeDriver()
        {
            var options = new ChromeOptions();
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless");
            var driverService = ChromeDriverService.CreateDefaultService();
            driverService.HideCommandPromptWindow = true;
            driver = new ChromeDriver(driverService, options);
        }
    }
}