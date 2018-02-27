/*
    Myrtille: A native HTML4/5 Remote Desktop Protocol client.

    Copyright(c) 2014-2017 Cedric Coste

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

        http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using Myrtille.Common.Interfaces;
using Myrtille.Helpers;

namespace Myrtille.Web
{
    public partial class Default : Page
    {
        protected RemoteSession RemoteSession;

        protected bool FileTransferDisabled = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["FileTransferDisabled"]);

        /// <summary>
        /// initialization
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(
            object sender,
            EventArgs e)
        {
            #region session fixation attack

            // prevent session fixation attack by generating a new session ID upon login
            // https://www.owasp.org/index.php/Session_Fixation
            if (!string.IsNullOrEmpty(HttpContext.Current.Request["oldSID"]))
            {
                try
                {
                    HttpContext.Current.Application.Lock();

                    // retrieve the given (old) http session
                    var httpSessions = (IDictionary<string, HttpSessionState>)HttpContext.Current.Application[HttpApplicationStateVariables.HttpSessions.ToString()];
                    var httpSession = httpSessions[HttpContext.Current.Request["oldSID"]];

                    // retrieve the remote session bound to it
                    var remoteSession = httpSession[HttpSessionStateVariables.RemoteSession.ToString()];

                    // unbind it from the old http session
                    httpSession[HttpSessionStateVariables.RemoteSession.ToString()] = null;

                    // bind it to the new http session
                    HttpContext.Current.Session[HttpSessionStateVariables.RemoteSession.ToString()] = remoteSession;

                    // cancel the old http session
                    httpSession.Abandon();

                    // unregister it at application level
                    httpSessions.Remove(httpSession.SessionID);
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to generate a new http session upon login ({0})", exc);
                }
                finally
                {
                    HttpContext.Current.Application.UnLock();
                }
            }

            #endregion

            try
            {
                // retrieve the active remote session, if any
                if (HttpContext.Current.Session[HttpSessionStateVariables.RemoteSession.ToString()] != null)
                {
                    try
                    {
                        RemoteSession = (RemoteSession)HttpContext.Current.Session[HttpSessionStateVariables.RemoteSession.ToString()];
                    }
                    catch (Exception exc)
                    {
                        System.Diagnostics.Trace.TraceError("Failed to retrieve the remote session for the http session {0}, ({1})", HttpContext.Current.Session.SessionID, exc);
                    }
                }

                // update controls
                UpdateControls();

                // disable the browser cache; in addition to a "noCache" dummy param, with current time, on long-polling and xhr requests
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.Cache.SetNoStore();
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to load myrtille ({0})", exc);
            }
        }

        /// <summary>
        /// force remove the .net viewstate hidden fields from page (large bunch of unwanted data in url)
        /// </summary>
        /// <param name="writer"></param>
        protected override void Render(
            HtmlTextWriter writer)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            var tw = new HtmlTextWriter(sw);
            base.Render(tw);
            var html = sb.ToString();
            html = Regex.Replace(html, "<input[^>]*id=\"(__VIEWSTATE)\"[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, "<input[^>]*id=\"(__VIEWSTATEGENERATOR)\"[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            writer.Write(html);
        }

        /// <summary>
        /// update the UI
        /// </summary>
        private void UpdateControls()
        {
            if (RemoteSession != null)
            {
                // login screen
                loginScreen.Visible = RemoteSession.State != RemoteSessionState.Connecting && RemoteSession.State != RemoteSessionState.Connected;

                // toolbar
                toolbar.Style["visibility"] = loginScreen.Visible ? "hidden" : "visible";
                toolbar.Style["display"] = loginScreen.Visible ? "none" : "block";
                serverInfo.Value = RemoteSession.ServerAddress;
                stat.Value = RemoteSession.StatMode ? "Hide Stat" : "Show Stat";
                stat.Disabled = loginScreen.Visible;
                debug.Value = RemoteSession.DebugMode ? "Hide Debug" : "Show Debug";
                debug.Disabled = loginScreen.Visible;
                browser.Value = RemoteSession.CompatibilityMode ? "HTML5" : "HTML4";
                browser.Disabled = loginScreen.Visible;
                scale.Value = RemoteSession.ScaleDisplay ? "Unscale" : "Scale";
                scale.Disabled = loginScreen.Visible;
                keyboard.Disabled = loginScreen.Visible;
                clipboard.Disabled = loginScreen.Visible;
                files.Disabled = loginScreen.Visible || FileTransferDisabled || (RemoteSession.ServerAddress.ToLower() != "localhost" && RemoteSession.ServerAddress != "127.0.0.1" && RemoteSession.ServerAddress != HttpContext.Current.Request.Url.Host && string.IsNullOrEmpty(RemoteSession.UserDomain)) || string.IsNullOrEmpty(RemoteSession.UserName) || string.IsNullOrEmpty(RemoteSession.UserPassword);
                cad.Disabled = loginScreen.Visible;
                mrc.Disabled = loginScreen.Visible;
                disconnect.Disabled = loginScreen.Visible;
            }
        }

        /// <summary>
        /// start the rdp session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void ConnectButtonClick(
            object sender,
            EventArgs e)
        {
            // remove the active remote session, if any (disconnected?)
            if (RemoteSession != null)
            {
                try
                {
                    // unset the remote session for the current http session
                    HttpContext.Current.Session[HttpSessionStateVariables.RemoteSession.ToString()] = null;
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to remove remote session ({0})", exc);
                }
                finally
                {
                    RemoteSession = null;
                }
            }

            // create a new remote session
            try
            {
                HttpContext.Current.Application.Lock();

                // auto-increment the remote sessions counter
                // note that it doesn't really count the active remote sessions... it's just an auto-increment for the remote session id, ensuring it's unique...
                var remoteSessionsCounter = (int)HttpContext.Current.Application[HttpApplicationStateVariables.RemoteSessionsCounter.ToString()];
                remoteSessionsCounter++;

                // create the remote session
                RemoteSession = new RemoteSession
                {
                    Id = remoteSessionsCounter,
                    State = RemoteSessionState.NotConnected,
                    ServerAddress = string.IsNullOrEmpty(server.Value) ? "localhost" : server.Value,
                    UserDomain = domain.Value,
                    UserName = user.Value,
                    UserPassword = string.IsNullOrEmpty(passwordHash.Value) ? password.Value : RDPCryptoHelper.DecryptPassword(passwordHash.Value),
                    ClientWidth = int.Parse(width.Value),
                    ClientHeight = int.Parse(height.Value),
                    Program = program.Value
                };

                // check if any of loaded AuthenticationPlugins can process the request
                foreach (var plugin in PluginManager.GetPlugins<IAuthenticationPlugin>())
                {
                    if (plugin.CanProcess(Request.Params.ToString()))
                    {
                        RemoteSession.UserDomain = plugin.Domain;
                        RemoteSession.UserName = plugin.UserName;
                        RemoteSession.UserPassword = plugin.Password;
                    }
                }

                // set the remote session for the current http session
                HttpContext.Current.Session[HttpSessionStateVariables.RemoteSession.ToString()] = RemoteSession;

                // register the http session at application level
                var httpSessions = (IDictionary<string, HttpSessionState>)HttpContext.Current.Application[HttpApplicationStateVariables.HttpSessions.ToString()];
                httpSessions[HttpContext.Current.Session.SessionID] = HttpContext.Current.Session;

                // update the remote sessions auto-increment counter
                HttpContext.Current.Application[HttpApplicationStateVariables.RemoteSessionsCounter.ToString()] = remoteSessionsCounter;
            }
            catch (Exception exc)
            {
                System.Diagnostics.Trace.TraceError("Failed to create remote session ({0})", exc);
                RemoteSession = null;
            }
            finally
            {
                HttpContext.Current.Application.UnLock();
            }

            // connect it
            if (RemoteSession != null)
            {
                try
                {
                    // update the remote session state
                    RemoteSession.State = RemoteSessionState.Connecting;

                    // create pipes for the web gateway and the rdp client to talk
                    RemoteSession.Manager.Pipes.CreatePipes();

                    // the rdp client does connect the pipes when it starts; when it stops (either because it was closed, crashed or because the rdp session had ended), pipes are released
                    // as the process command line can be displayed into the task manager / process explorer, the connection settings (including user credentials) are now passed to the rdp client through the inputs pipe
                    // use http://technet.microsoft.com/en-us/sysinternals/dd581625 to track the existing pipes
                    RemoteSession.Manager.Client.StartProcess(
                        RemoteSession.Id,
                        RemoteSession.ClientWidth,
                        RemoteSession.ClientHeight);

                    // update controls
                    UpdateControls();
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to connect the remote session {0} ({1})", RemoteSession.Id, exc);
                }
            }
        }

        /// <summary>
        /// stop the rdp session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DisconnectButtonClick(
            object sender,
            EventArgs e)
        {
            // disconnect the active remote session, if any and connecting/connected
            if (RemoteSession != null && (RemoteSession.State == RemoteSessionState.Connecting || RemoteSession.State == RemoteSessionState.Connected))
            {
                try
                {
                    // update the remote session state
                    RemoteSession.State = RemoteSessionState.Disconnecting;

                    // send a disconnect command to the rdp client
                    RemoteSession.Manager.SendCommand(RemoteSessionCommand.CloseRdpClient);

                    // update controls
                    UpdateControls();
                }
                catch (Exception exc)
                {
                    System.Diagnostics.Trace.TraceError("Failed to disconnect the remote session {0} ({1})", RemoteSession.Id, exc);
                }
            }
        }
    }
}