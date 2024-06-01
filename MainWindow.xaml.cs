using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Discord;
using System.Threading;

namespace BiliCord
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Stack<Uri> history = new Stack<Uri>();

        public MainWindow()
        {
            InitializeComponent();
            InitialiseWebView();
            InitialiseDiscord();
        }

        private void InitialiseDiscord()
        {
            var clientID = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (clientID == null)
            {
                clientID = "418559331265675294";
            }
            var discord = new Discord.Discord(Int64.Parse(clientID), (UInt64)Discord.CreateFlags.Default);
            discord.RunCallbacks();
            discord.SetLogHook(Discord.LogLevel.Debug, LogProblemFunctions);
            var applicationManager = discord.GetApplicationManager();
            var userManager = discord.GetUserManager();
            var imageManager = discord.GetImageManager();

            userManager.OnCurrentUserUpdate += () =>
            {
                var currentUser = userManager.GetCurrentUser();
                Trace.WriteLine(currentUser.Username);
                Trace.WriteLine(currentUser.Id);
            };

            var activityManager = discord.GetActivityManager();
            var lobbyManager = discord.GetLobbyManager();

            activityManager.RegisterCommand();
            var activity = new Discord.Activity
            {
                State = "Watching Bilibili",
                Details = "Watching Bilibili",
            };
            Trace.WriteLine("Discord Rich Presence Initialised");
            activityManager.UpdateActivity(activity, (res) =>
            {
                if (res == Discord.Result.Ok)
                {
                    Trace.WriteLine("Discord Rich Presence Updated");
                }
                else
                {
                    Trace.WriteLine("Discord Rich Presence Update Failed");
                }
            });

            // Pump the event look to ensure all callbacks continue to get fired.
            Thread discordThread = new Thread(() => DiscordLoop(discord, lobbyManager));
            discordThread.IsBackground = true;
            discordThread.Start();

        }

        private void DiscordLoop(Discord.Discord discord, LobbyManager lobbyManager)
        {

            try
            {
                while (true)
                {
                    discord.RunCallbacks();
                    lobbyManager.FlushNetwork();
                    Thread.Sleep(1000 / 60);
                }
            }
            finally
            {
                discord.Dispose();
            }
        }

        public void LogProblemFunctions(Discord.LogLevel level, string message)
        {
            String logLevel = level.ToString();
            String logMessage = message;
            Trace.WriteLine("Discord:{0} - {1}" + logLevel + logMessage);
        }

        public async void InitialiseWebView()
        {
            await webView.EnsureCoreWebView2Async(null);

            String url = webView.Source.ToString();
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;

            // subscribe to the NavigationStarting event
            webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
        }


        private void CoreWebView2_NewWindowRequested(object sender,
            CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.NewWindow = (CoreWebView2)sender;
            e.Handled = true;
        }

        private void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            String url = webView.Source.ToString();
            history.Push(new Uri(url));
        }

        private void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Log or handle navigation starting if needed
        }

        private void CoreWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Check and update CanGoBack state when navigation is completed
            CommandManager.InvalidateRequerySuggested();
        }

        void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (history.Count > 1)
            {
                history.Pop(); // Pop the current page
                Uri uri = history.Pop(); // Pop the previous page
                webView.CoreWebView2.Navigate(uri.ToString());
            }
        }

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // Enable the button only if the WebView can navigate back
            e.CanExecute = history.Count > 1;
        }

    }
}
