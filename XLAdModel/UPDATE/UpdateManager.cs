using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using WinSCP;

namespace XLAdModel.UPDATE {
    public class UpdateManager {

        private readonly string _ftpPath = "waws-prod-sn1-065.ftp.azurewebsites.windows.net";
        private readonly string _ftpfullpath = "ftp://" + "waws-prod-sn1-065.ftp.azurewebsites.windows.net" + "/XLAdModel.vsto";         // ftp://waws-prod-sn1-065.ftp.azurewebsites.windows.net
        private readonly string _ftpPW = "T4flAygiZ6SST3vnltKf67T2qNY21g7pgygsFDdJbyq3xjx2tabFgPZwCnM3";
        private readonly string _ftpLogin = @"XLAdModel\$XLAdModel";
        private readonly string _inputfilepath; //ex: @"C:\Users\SB13\OneDrive\XLAppPublish\XLAppAddIn.vsto";   //string inputfilepath = @"C:\Temp\FileName.exe";

        private readonly bool _updateIsAvail;
        private readonly string _currentVersion;

        public UpdateManager() {
            if (ApplicationDeployment.IsNetworkDeployed)
                this._inputfilepath = ApplicationDeployment.CurrentDeployment.UpdateLocation.AbsolutePath.ToString();

            this._currentVersion = GetCurrentVersion();
            this._updateIsAvail = CheckIfUpdateAvailable();

            if (this._updateIsAvail) {
                Globals.ThisAddIn.Application.Cursor = Microsoft.Office.Interop.Excel.XlMousePointer.xlWait;

                string result = InstallUpdateSyncWithInfo();
                if (result != null)
                    MessageBox.Show("Nouvelle version : " + result +"\n\nL'application va maintenant quitter.",ThisAddIn.NameOfAddin);

                Globals.ThisAddIn.Application.Cursor = Microsoft.Office.Interop.Excel.XlMousePointer.xlDefault;
                Globals.ThisAddIn.QuitAddIn();
            }
                

        }


        // Téléchargé et Lire sur le site FTP le document XML qui nous signalera si une MAJ est dispo.
        private bool CheckIfUpdateAvailable() {
            if (!ApplicationDeployment.IsNetworkDeployed)
                return false;

            bool updateAvail = false;

            try {
                using (System.Net.WebClient request = new System.Net.WebClient()) {
                    request.Credentials = new System.Net.NetworkCredential(_ftpLogin, _ftpPW);
                    byte[] fileData = request.DownloadData(_ftpfullpath);

                    using (FileStream file = File.Create(_inputfilepath)) {
                        file.Write(fileData, 0, fileData.Length);
                        file.Close();
                    }

                    XmlDocument doc = new XmlDocument();
                    doc.Load(ApplicationDeployment.CurrentDeployment.UpdateLocation.AbsolutePath); //doc.Load(@"C:\Users\SB13\OneDrive\XLAppPublish\XLAppAddIn.vsto");
                    string flder = doc.GetElementsByTagName("dependentAssembly")[0].Attributes[1].Value.Split('\\')[1].ToString(); // retourne "XLAppAddIn_1_0_0_139"
                    string[] versionT = flder.Split('_');
                    string version = "";
                    for (int i = 1, l = versionT.Length; i < l; i++)
                        version += versionT[i] + ".";
                    version = version.Remove(version.Length - 1);

                    if (version != this._currentVersion)
                        return true;

                }
            } catch (Exception e) {
                MessageBox.Show("La requête FTP afin d'obtenir la version la plus à jour disponible a échoué. \n\n" + e.Message);
            }

            return updateAvail;
        }
        private string GetCurrentVersion() {
            return ApplicationDeployment.IsNetworkDeployed
           ? ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() // retourne la bonne version en exécution
           : Assembly.GetExecutingAssembly().GetName().Version.ToString(); //le 2e retourne : 1.0.0.0

        }
        private string GetClickOnceLocation() {
            //Get the assembly information
            System.Reflection.Assembly assemblyInfo = System.Reflection.Assembly.GetExecutingAssembly();
            //Location is where the assembly is run from 
            string assemblyLocation = assemblyInfo.Location;
            //CodeBase is the location of the ClickOnce deployment files
            Uri uriCodeBase = new Uri(assemblyInfo.CodeBase);
            return Path.GetDirectoryName(uriCodeBase.LocalPath.ToString());
        }



        //     https://blogs.msdn.microsoft.com/krimakey/2008/04/18/click-once-forced-updates-in-vsto-ii-a-fuller-solution/
        public string InstallUpdateSyncWithInfo() {
            // https://msdn.microsoft.com/en-us/library/ms404263.aspx
            UpdateCheckInfo info = null;

            //if (!ApplicationDeployment.IsNetworkDeployed)
            //    return "La version actuelle n'est pas déployé en réseau ou est une version de développement.";

            if (ApplicationDeployment.IsNetworkDeployed) {

                //TEST SASHA
                // downloadFileFTPForUpdate();
                //FIN TEST

                Assembly addinAssembly = Assembly.GetExecutingAssembly();

                string CachePath = addinAssembly.CodeBase.Substring(0, addinAssembly.CodeBase.Length -
                    System.IO.Path.GetFileName(addinAssembly.CodeBase).Length);

                ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                // https://blogs.msdn.microsoft.com/krimakey/2008/04/10/click-once-forced-updates-in-vsto-some-things-we-dont-recommend-using-that-you-might-consider-anyway/
                ApplicationIdentity appId = new ApplicationIdentity(ad.UpdatedApplicationFullName);

                PermissionSet unrestrictedPerms = new PermissionSet(PermissionState.Unrestricted);

                ApplicationTrust appTrust = new ApplicationTrust(appId) {
                    DefaultGrantSet = new PolicyStatement(unrestrictedPerms),
                    IsApplicationTrustedToRun = true,
                    Persist = true
                };

                ApplicationSecurityManager.UserApplicationTrusts.Add(appTrust);

                retry:
                try {
                    info = ad.CheckForDetailedUpdate();

                } catch (DeploymentDownloadException dde) {
                    // À CE STADE, ON A TÉLÉCHARGÉ LE FICHIER .VSTO // SI UNE MISE À JOUR EST DISPONIBLE, L'ERREUR EST ATTRAPÉ ICI.

                    //MessageBox.Show("The new version of the application cannot be downloaded at this time. \n\nPlease check your network connection, or try again later. Error: " + dde.Message); //MessageBox.Show("La nouvelle version de l'application ne peut être télécharger en ce moment. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde.Message);

                    //return "La nouvelle version de l'application ne peut être télécharger en ce moment. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde.Message;
                    DialogResult result = MessageBox.Show("Bienvenu dans l'application " + ThisAddIn.NameOfAddin + "! Une nouvelle version de l'application est disponible et doit être téléchargé. La durée du téléchargement est d'une quinzaine (15) de secondes. \n\nSouhaitez-vous la télécharger maintenant?", ThisAddIn.NameOfAddin + " - Mise à jour disponible", MessageBoxButtons.YesNo);

                    if (result == DialogResult.Yes) {
                        try {
                            if (downloadFTPfolderForUpdate())
                                goto retry;
                            else
                                return "La nouvelle version de l'application ne peut être télécharger en ce moment. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde.Message;
                        } catch (Exception e) {
                            return "La nouvelle version de l'application ne peut être télécharger en ce moment. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde.Message + "\n\n" + e.Message;
                        }

                    } else {
                        return "Vous avez choisi de ne pas télécharger la mise à jour qui est disponible en ce moment. \n\nVeuillez réeesayer plus tard, ou communiquez avec le support pour plus d'informations.";
                    }


                } catch (InvalidDeploymentException ide) {
                    //MessageBox.Show("Cannot check for a new version of the application. The ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
                    //MessageBox.Show("Impossible de vérifier pour une nouvelle version de l'application. Le déploiement ClickOnce de l'application est corrompue. Veuillez redéployez l'application et réessayer. Erreur: " + ide.Message);
                    return "Impossible de vérifier pour une nouvelle version de l'application. Le déploiement ClickOnce de l'application est corrompue. Veuillez redéployez l'application et réessayer. Erreur: " + ide.Message;
                } catch (InvalidOperationException ioe) {
                    //MessageBox.Show("This application cannot be updated. It is likely not a ClickOnce application. Error: " + ioe.Message);
                    //MessageBox.Show("Cet application ne peut être mise à jour. Ce n'est vraisemblablement pas une application ClickOnce. Erreur: " + ioe.Message);
                    return "Cet application ne peut être mise à jour. Ce n'est vraisemblablement pas une application ClickOnce. Erreur: " + ioe.Message;
                }

                if (!info.UpdateAvailable)
                    return "La version actuelle (" + (DateTime.Now.Year % 100).ToString() + "." + ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() + ") est à jour.";

                if (info.UpdateAvailable) {
                    Boolean doUpdate = true;

                    //  string test = CurrentDep.UpdatedVersion.ToString();

                    if (!info.IsUpdateRequired) {
                        //DialogResult dr = MessageBox.Show("An update is available. Would you like to update the application now?", "Update Available", MessageBoxButtons.OKCancel);
                        DialogResult dr = MessageBox.Show("La mise à jour de l'application est téléchargé et disponible. Souhaitez-vous l'exécuter maintenant?", ThisAddIn.NameOfAddin + " - Mise à jour disponible", MessageBoxButtons.OKCancel);
                        if (!(DialogResult.OK == dr)) {
                            doUpdate = false;
                            return "Mise à jour annulée."; //
                        }
                    } else {
                        // Display a message that the app MUST reboot. Display the minimum required version.
                        MessageBox.Show("This application has detected a mandatory update from your current " +
                            "version to version " + info.MinimumRequiredVersion.ToString() +
                            ". The application will now install the update and restart.",
                            "Update Available", MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    if (doUpdate) {
                        try {
                            // ad.Update(); // Enlèvement SB
                            Uri DocPath = new Uri(Globals.ThisAddIn.Application.Path + "\\" + Globals.ThisAddIn.Application.Name); //test sb
                            Uri InstallerPath = new Uri("C:\\Program Files\\Common Files\\microsoft shared\\VSTO\\10.0\\VSTOINSTALLER.exe"); //test sb
                                                                                                                                             // Uri RestarterPath = new Uri(CachePath + "WordRestarter.exe"); //enlève sb
                            Uri Updatelocation = new Uri(ad.UpdateLocation.ToString());

                            //Call VSTOInstaller Explicitely in "Silent Mode"
                            Process VstoInstallerProc = new System.Diagnostics.Process();
                            VstoInstallerProc.StartInfo.Arguments = " /S /I " + Updatelocation.AbsoluteUri;
                            VstoInstallerProc.StartInfo.FileName = InstallerPath.AbsoluteUri;
                            VstoInstallerProc.Start();

                            VstoInstallerProc.WaitForExit();
                            if (VstoInstallerProc.ExitCode == 0) {
                                string updatedVersDL = (DateTime.Now.Year % 100).ToString() + "." + ad.UpdatedVersion.ToString();
                                MessageBox.Show("La mise à jour de l'application à la version " + updatedVersDL + " a été réussi et sera effective au prochain redémarrage de l'application. Veuillez redémarrer l'application maintenant.", "Mise à jour - Version " + updatedVersDL);
                                return updatedVersDL;
                            } else {
                                //  MessageBox.Show("Échec de mise à jour: Exit Code (" + VstoInstallerProc.ExitCode.ToString() + ")");
                                return "Échec de mise à jour: Exit Code (" + VstoInstallerProc.ExitCode.ToString() + ")";
                            }


                            //Call VSTOInstaller Explicitely in "Silent Mode"
                            // Process RestarterProc = new System.Diagnostics.Process();
                            // RestarterProc.StartInfo.Arguments = DocPath.AbsoluteUri;
                            // RestarterProc.StartInfo.FileName = RestarterPath.AbsoluteUri;
                            //  RestarterProc.Start();


                            //MessageBox.Show("The application has been upgraded, and will now restart.");
                            //MessageBox.Show("La mise à jour de l'application a été réussi et sera effective au prochain redémarrage.");

                            //Application.Restart(); MODIF SB ENLÈVEMENT !
                        } catch (DeploymentDownloadException dde) {
                            //MessageBox.Show("Cannot install the latest version of the application. \n\nPlease check your network connection, or try again later. Error: " + dde);
                            // MessageBox.Show("Échec d'installation de la plus récente mise à jour. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde);
                            return "Échec d'installation de la plus récente mise à jour. \n\nVeuillez vérifier votre connection, ou réessayer plus tard. Erreur: " + dde;
                        }
                    }
                }
            }
            return "";
        }

        private bool downloadFTPfolderForUpdate() {
            try {
                
                ApplicationDeployment CurrentDep = ApplicationDeployment.CurrentDeployment;

                XmlDocument doc = new XmlDocument();
                doc.Load(CurrentDep.UpdateLocation.AbsolutePath); //doc.Load(@"C:\Users\SB13\OneDrive\XLAppPublish\XLAppAddIn.vsto");
                string flder = doc.GetElementsByTagName("dependentAssembly")[0].Attributes[1].Value.Split('\\')[1].ToString(); // retourne "XLAppAddIn_1_0_0_139"

                var vstoPath = Path.GetDirectoryName(CurrentDep.UpdateLocation.AbsolutePath);

                var vstoAppFilePath = vstoPath + @"\Application Files";
                vstoAppFilePath += @"\" + flder;

                System.IO.Directory.CreateDirectory(vstoAppFilePath); // If the folder does not exist yet, it will be created. If the folder exists already, the line will be ignored.
                string ftpFolderpath = "Application Files/" + flder + "/*";

                // Avec WINSCP
                // Setup session options
                SessionOptions sessionOptions = new SessionOptions {
                    Protocol = Protocol.Ftp,
                    HostName = _ftpPath,
                    UserName = _ftpLogin,
                    Password = _ftpPW,
                };
                // HostName = "waws-prod-yq1-003.ftp.azurewebsites.windows.net",
                // UserName = @"XLAppFTP\$XLAppFTP",
                // Password = "7sWxu1wkPMCqFkCf9ms7NGZwezQ0wnrDGgee7HTDltn0d8wFNxnC4Ae4TA81",

                using (Session session = new Session()) {
                    // Connect
                    session.Open(sessionOptions);

                    // Download files
                    session.GetFiles(ftpFolderpath, vstoAppFilePath + @"\*").Check();  //session.GetFiles("/directory/to/download/*", @"C:\target\directory\*").Check();
                }


                return true;
            } catch (Exception e) {
                MessageBox.Show("Download folder exception \n\n" + e.Message);
                return false;
            }

        }


    }
}
