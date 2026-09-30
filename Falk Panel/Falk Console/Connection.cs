using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falk_Console
{
    public static class Connection
    {
        public static CrmServiceClient CreateConnection()
        {
            //Step 1 - Retrieving CRM Essential Information.
            string sEnvironment = "https://falk-uat.crm.dynamics.com/";
            string sUserKey = "crmintegration@falkpanel.com";
            string sUserPassword = "TechronusCRM24426";

            //Step 2- Creating A Connection String.
            string conn = $@" Url = {sEnvironment};AuthType = OAuth;UserName = {sUserKey}; Password = {sUserPassword};AppId = 51f81489-12ee-4a9e-aaae-a2591f45987d;RedirectUri = app://58145B91-0C36-4500-8554-080854F2AC97;LoginPrompt=Auto; RequireNewInstance = True";
            Console.WriteLine("Operating Environment : " + sEnvironment);

            return new CrmServiceClient(conn);
        }
    }
}