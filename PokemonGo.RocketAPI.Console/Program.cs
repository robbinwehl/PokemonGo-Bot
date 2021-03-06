using Google.Protobuf;
using POGOProtos.Enums;
using PokemonGo.RocketAPI.Console.Helper;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.HttpClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Device.Location;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PokemonGo.RocketAPI.Logic.Shared;
using PokemonGo.RocketAPI.Logic.Utils;

namespace PokemonGo.RocketAPI.Console
{
    internal class Program
    {
        [DllImport("kernel32.dll")]
        public static extern Boolean FreeConsole();
        
        public static string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configs");
        public static string path_translation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translations");
        public static string path_device = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Device");
        public static string lastcords = Path.Combine(path, "LastCoords.txt");
        public static string huntstats = Path.Combine(path, "HuntStats.txt");
        public static string deviceSettings = Path.Combine(path_device, "DeviceInfo.txt");
        public static string cmdCoords = string.Empty;
        public static string accountProfiles = Path.Combine(path, "Profiles.txt");
        static string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        public static string pokelog = Path.Combine(logPath, "PokeLog.txt");
        public static string manualTransferLog = Path.Combine(logPath, "TransferLog.txt");
        public static string EvolveLog = Path.Combine(logPath, "EvolveLog.txt");
        public static string path_pokedata = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PokeData");       

        static void SharePokesniperURI(string uri)
        {
            try 
            {
                var filename = Path.GetTempPath()+"pokesniper";
                if (File.Exists(filename)){
                    MessageBox.Show("There is a pending pokemon.\nTry latter");
                }
                var stream = new FileStream(filename,FileMode.OpenOrCreate);
                var writer = new BinaryWriter(stream,new UTF8Encoding());
                writer.Write(uri);
                stream.Close();
            } 
            catch (Exception e) 
            {
                MessageBox.Show(e.ToString());
            }
        }
        [STAThread]
        static void Main(string[] args)
        {

            // Review & parse command line arguments
            var BotVersion = new Version(Assembly.GetExecutingAssembly().GetName().Version.ToString());

            if (args != null && args.Length > 0)
            {
                #region Parse Arguments
                // First of all.
                // We check if bot have called clicking in a pokesnimer URI: pokesniper2://PokemonName/latitude,longitude
                if (args[0].Contains("pokesniper2"))
                {
                    // If yes, We create a temporary file to share with main process, and close.
                    SharePokesniperURI(args[0]);
                    return;
                }
                foreach (string arg in args)
                {
                    if (arg.Contains(","))
                    {
                        Logger.ColoredConsoleWrite(ConsoleColor.Green, $"Found coordinates in command line: {arg}");
                        if (File.Exists(lastcords))
                        {
                            Logger.ColoredConsoleWrite(ConsoleColor.Yellow, "Last coords file exists, trying to delete it");
                            File.Delete(lastcords);
                        }
                        cmdCoords = arg;
                    }

                    if (arg.ToLower().Contains("-bypassversioncheck"))
                        GlobalVars.BypassCheckCompatibilityVersion = true;

                    if (arg.ToLower().Contains("-help"))
                    {
                        //Show Help
                        System.Console.WriteLine($"Pokemon BOT C# v{BotVersion.ToString()} help" + Environment.NewLine);
                        System.Console.WriteLine("Use:");
                        System.Console.WriteLine("  -nogui <lat>,<long>         Console mode only, starting on the indicated Latitude & Longitude");
                        System.Console.WriteLine("  -bypassversioncheck         to NOT check BOT & API compatibility (be careful with that option)");
                        System.Console.WriteLine("  -help                       this help" + Environment.NewLine);
                        Environment.Exit(0);
                    }
                }
                #endregion
            }


            // Checking if current BOT API implementation supports NIANTIC current API (unless there's an override command line switch)
            if (!GlobalVars.BypassCheckCompatibilityVersion)
            {
                Logger.ColoredConsoleWrite(ConsoleColor.DarkMagenta, $"Bot Current version: {BotVersion}");
                Logger.ColoredConsoleWrite(ConsoleColor.DarkMagenta, $"Bot Supported API version: {GlobalVars.BotApiSupportedVersion}");
                Logger.ColoredConsoleWrite(ConsoleColor.DarkMagenta, $"Current API version: {new CurrentAPIVersion().GetNianticAPIVersion()}");

                bool CurrentVersionsOK = new CurrentAPIVersion().CheckAPIVersionCompatibility( GlobalVars.BotApiSupportedVersion);
                if (!CurrentVersionsOK)
                {
                    Logger.ColoredConsoleWrite(ConsoleColor.Red, $"Atention, current API version is new and still not supported by Bot.");
                    Logger.ColoredConsoleWrite(ConsoleColor.Red, $"Bot will now exit to keep your account safe.");
                    Logger.ColoredConsoleWrite(ConsoleColor.Red, $"---------- PRESS ANY KEY TO CLOSE ----------");

                    System.Console.ReadKey();
                    Environment.Exit(-1);
                }
            }

            // Check if Bot is deactivated at server level
            StringUtils.CheckKillSwitch();

            var openGUI = false;

            if (args != null && args.Length > 0 && args[0].Contains("-nogui"))
            {
                Logger.ColoredConsoleWrite(ConsoleColor.Red, "You added -nogui!");
                
                #region Read bot settings
                if (File.Exists(Program.accountProfiles))
                {
                    string JSONstring = File.ReadAllText(accountProfiles);
                    var Profiles = Newtonsoft.Json.JsonConvert.DeserializeObject<Collection<Profile>>(JSONstring);
                    Profile selectedProfile = null;
                    foreach (Profile _profile in Profiles)
                    {
                        if (_profile.IsDefault)
                        {
                            selectedProfile = _profile;
                            break;
                        }
                    }
                    if (selectedProfile != null)
                    {
                        var filenameProf= Path.Combine(path, $"{selectedProfile.ProfileName}.json");
                        selectedProfile.Settings = ProfileSettings.LoadFromFile(filenameProf);
                        selectedProfile.Settings.SaveToGlobals();
                    }
                    else
                    {
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "Default Profile not found. You didn't setup the bot correctly.");
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "Run it without -nogui to Configure.");
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "Exiting..");
                        Environment.Exit(-1);
                    }
                }
                else
                {
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "You are not setup the bot yet.");
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "Run it without -nogui to Configure.");
                        Logger.ColoredConsoleWrite(ConsoleColor.Red, "Exiting..");
                        Environment.Exit(-1);
                }

                if (GlobalVars.UsePwdEncryption)
                {
                    GlobalVars.Password = Encryption.Decrypt(GlobalVars.Password);
                }
                #endregion
                
                if (cmdCoords != string.Empty)
                {
                    string[] crdParts = cmdCoords.Split(',');
                    GlobalVars.latitude = double.Parse(crdParts[0].Replace(',', '.'), GUI.cords, System.Globalization.NumberFormatInfo.InvariantInfo);
                    GlobalVars.longitude = double.Parse(crdParts[1].Replace(',', '.'), GUI.cords, System.Globalization.NumberFormatInfo.InvariantInfo);
                }

                Logger.ColoredConsoleWrite(ConsoleColor.Yellow, $"Starting at: {GlobalVars.latitude},{GlobalVars.longitude}");
            }
            else
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new GUI());
                Task.Run(() =>
                {
                             new Panels.SplashScreen().ShowDialog();
                      });
                openGUI = GlobalVars.EnablePokeList;
                // To open tabbed GUI to test programing 
                /*Application.Run( new Pokemons()); 
                Environment.Exit(0);*/  
            }


            SleepHelper.PreventSleep();
            CreateLogDirectories();

            GlobalVars.infoObservable.HandleNewHuntStats += SaveHuntStats;

            CheckVersion(); // Check if a new version of BOT is available
            Task.Run(() =>
            {
               do
               {
                    try
                    {
                        new Logic.Logic(new Settings(), GlobalVars.infoObservable).Execute();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error( $"Unhandled exception: {ex}");
                    }
                    Logger.Error("Restarting in 20 Seconds.");
                    Thread.Sleep(20000);
               }while (Logic.Logic.restartLogic);
            });

            if (openGUI)
            {
                if (GlobalVars.simulatedPGO)
                {
                    Application.Run( new GameAspectSimulator());
                }
                else
                {
                    if (GlobalVars.EnableConsoleInTab)
                        FreeConsole();
                    Application.Run( new Pokemons());
                }
            }
            else
            {
                   System.Console.ReadLine();
            }
            SleepHelper.AllowSleep();
        }

        private static void SaveHuntStats(string newHuntStat)
        {
            File.AppendAllText(huntstats, newHuntStat);
        }

        public static void CheckVersion()
        {
            try
            {
                var match =
                    new Regex(
                        @"\[assembly\: AssemblyVersion\(string.Empty(\d{1,})\.(\d{1,})\.(\d{1,})\.(\d{1,})string.Empty\)\]")
                        .Match(DownloadServerVersion());

                if (!match.Success) return;
                var gitVersion =
                    new Version(
                        $"{match.Groups[1]}.{match.Groups[2]}.{match.Groups[3]}.{match.Groups[4]}");
                if (gitVersion <= Assembly.GetExecutingAssembly().GetName().Version)
                {
                    //ColoredConsoleWrite(ConsoleColor.Yellow, "Awesome! You have already got the newest version! " + Assembly.GetExecutingAssembly().GetName().Version);
                    return;
                }

                Logger.ColoredConsoleWrite(ConsoleColor.Red, "There is a new Version available: " + gitVersion);
                Logger.ColoredConsoleWrite(ConsoleColor.Red, "Its recommended to use the newest Version.");
                if (cmdCoords == string.Empty)
                {
                    Logger.ColoredConsoleWrite(ConsoleColor.Red, "Starting in 10 Seconds.");
                    Thread.Sleep(10000);
                }
                else
                {
                    Logger.ColoredConsoleWrite(ConsoleColor.Yellow, "Starting right away because we are probably sniping.");
                }
            }
            catch (Exception)
            {
                Logger.ColoredConsoleWrite(ConsoleColor.White, "Unable to check for updates now...");
            }
        }

        public static Version getNewestVersion()
        {
            try
            {
                var match = DownloadServerVersion();

                var gitVersion = new Version(match);

                return gitVersion;

            }
            catch (Exception)
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        public static string DownloadServerVersion()
        {
            using (var wC = new WebClient())
                return
                    wC.DownloadString(
                        "https://raw.githubusercontent.com/Logxn/PokemonGo-Bot/master/ver.md");
        }

        private static void CreateLogDirectories()
        {
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }
            if (!File.Exists(pokelog))
            {
                File.Create(pokelog).Close();
            }
            if (!File.Exists(manualTransferLog))
            {
                File.Create(manualTransferLog).Close();
            }
            if (!File.Exists(EvolveLog))
            {
                File.Create(EvolveLog).Close();
            }
        }
    }
}
