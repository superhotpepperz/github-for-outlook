﻿using System;
using System.Diagnostics;
using System.Windows.Input;
using Analects.SettingsService;
using GithubForOutlook.Logic.Models;
using NGitHub;
using NGitHub.Authentication;
using Newtonsoft.Json;
using RestSharp;
using VSTOContrib.Core.Wpf;

namespace GithubForOutlook.Logic.Modules.Settings
{
    // TODO: is there a "Loaded" event in VSTOContrib to mimic the Activate/Deactivate hooks inside CM?

    public class SettingsViewModel : OfficeViewModelBase
    {
        readonly IGitHubOAuthAuthorizer authorizer;
        readonly IGitHubClient client;
        readonly ISettingsService settingsService;
        readonly ApplicationSettings settings;

        public SettingsViewModel(
            IGitHubOAuthAuthorizer authorizer, 
            IGitHubClient client, 
            ISettingsService settingsService, 
            ApplicationSettings settings)
        {
            this.authorizer = authorizer;
            this.client = client;
            this.settingsService = settingsService;
            this.settings = settings;
        }

        private bool trackIssues;
        public bool TrackIssues
        {
            get { return trackIssues; }
            set
            {
                trackIssues = value;
                RaisePropertyChanged(() => TrackIssues);
            }
        }

        private bool trackPullRequests;
        public bool TrackPullRequests
        {
            get { return trackPullRequests; }
            set
            {
                trackPullRequests = value;
                RaisePropertyChanged(() => TrackPullRequests);
            }
        }

        private User user;
        private bool showAuthenticateButton;
        private string authenticationSecret;

        public User User
        {
            get { return user; }
            set
            {
                user = value;
                RaisePropertyChanged(() => User);
            }
        }

        public ICommand SignInCommand { get { return new DelegateCommand(SignIn); } }

        public void SignIn()
        {
            // TODO: settings provider
            // TODO: landing page at Code52 to get user to paste auth credentials in

            var url = authorizer.BuildAuthenticationUrl(settingsService.Get<string>("client"), settingsService.Get<string>("redirect"));
            Process.Start(url);

            ShowAuthenticateButton = true;
        }

        public bool ShowAuthenticateButton
        {
            get { return showAuthenticateButton; }
            set
            {
                showAuthenticateButton = value;
                RaisePropertyChanged(() => ShowAuthenticateButton);
                AuthenticateCommand.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand AuthenticateCommand { get { return new DelegateCommand(Authenticate, CanAuthenticate); } }

        private bool CanAuthenticate()
        {
            return !string.IsNullOrWhiteSpace(AuthenticationSecret);
        }

        public string AuthenticationSecret
        {
            get { return authenticationSecret; }
            set
            {
                authenticationSecret = value;
                RaisePropertyChanged(() => AuthenticationSecret);
                AuthenticateCommand.RaiseCanExecuteChanged();
            }
        }

        private void Authenticate()
        {
            var request = new RestRequest("https://github.com/login/oauth/access_token", Method.POST);

            request.AddParameter("client_id", settingsService.Get<string>("client"));
            request.AddParameter("redirect_uri", settingsService.Get<string>("redirect"));
            request.AddParameter("client_secret", settingsService.Get<string>("secret"));
            request.AddParameter("code", AuthenticationSecret);

            var restClient = new RestClient();
            restClient.ExecuteAsync(request, OnAuthenticateCompleted);
        }

        private void OnAuthenticateCompleted(RestResponse arg1, RestRequestAsyncHandle arg2)
        {
            try
            {
                var result = JsonConvert.DeserializeObject<dynamic>(arg1.Content);
                string token = result.access_token;
                settings.AccessToken = token;
                settingsService.Save();
                client.Authenticator = new OAuth2UriQueryParameterAuthenticator(token);
                client.Users.GetAuthenticatedUserAsync(MapUser, LogError);
            }
            catch (Exception ex)
            {
                // TODO: notify that the code was not successful
            }
        }

        private void LogError(GitHubException obj)
        {

        }

        private void MapUser(NGitHub.Models.User obj)
        {
            User = new User
            {
                Name = obj.Login,
                Icon = obj.AvatarUrl
            };
        }

        public ICommand ClearCommand { get { return new DelegateCommand(Clear); } }

        public void Clear()
        {
            User = null;
        }
    }
}
