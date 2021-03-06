﻿using Multichain.Controllers;
using Multichain.Models.Database;
using Multichain.Models.Enum;
using MultiChain.Models.MultichainManager;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Web.Configuration;

namespace Multichain.Models.Services.Multichain
{
    public class MultichainServices:IMultichainServices
    {

        private MultichainController _multichainControler;
        DBEntity database;

        private string chainName;
        private string rpcName;
        private string rpcPassword;
        private string nodeIP;
        private int port;

        private Account account;
        private JsonRpcClient jsonRPCClient;
        private AddressManager addressManager;
        private PermissionManager permissionManager;
        private AssetManager assetManager;
        private TransactionManager transactionManager;
              
        public MultichainServices()
        {
            this.database = new DBEntity();

            chainName = WebConfigurationManager.AppSettings["chainname"];
            rpcName = WebConfigurationManager.AppSettings["rpc_userame"];
            rpcPassword = WebConfigurationManager.AppSettings["rpc_password"];
            nodeIP = WebConfigurationManager.AppSettings["networdid"];
            port = Convert.ToInt32(WebConfigurationManager.AppSettings["port"]);

            jsonRPCClient = new JsonRpcClient(
                rpcName: rpcName, 
                rpcPassword: rpcPassword,
                nodeIp: nodeIP,
                nodePort: port, 
                chainName: chainName
            );

            addressManager = new AddressManager(jsonRPCClient);
            permissionManager = new PermissionManager(jsonRPCClient);
            assetManager = new AssetManager(jsonRPCClient);
            transactionManager = new TransactionManager(jsonRPCClient);
        }

        public void SetRequset(MultichainController multichainControler)
        {
            this._multichainControler = multichainControler;
            this.account = AccountRequest();
        }

        private Account AccountRequest()
        {
            var bearerToken = _multichainControler.Request.Headers.Authorization.ToString().Substring(7);
            var account = database.Accounts.SingleOrDefault(acc => acc.beartoken == bearerToken);
            return account;
        }

        public object ImportAddress()
        {
            var address = CreateAddress();
            if (account != null)
            {
                address.email = account.email;
                database.Addresses.Add(address);
                database.SaveChanges();
                return new { email = address.email, address = address.addr};
            }
            else
                return Properties.Resources.AccountNotFound;
        }

        public Address CreateAddress()
        {
            var json = JObject.Parse(addressManager.CreateKeypairs());

            var address = json["result"][0]["address"].ToString();
            var privateKey = json["result"][0]["privkey"].ToString();

            addressManager.ImportAddress(address);
            //addressManager.ImportPrivateKey(privateKey);

            return new Address { addr = address, privateKey = privateKey };
        }

        public object GrantPermisstion(GrantPermissionInput input)
        { 
            if (!AuthAccountWithBearerToken(input.Address))
                return
                    Properties.Resources.UnValidAddress;

            var permission = ReadGrantFromBody(input);

            var json = permissionManager.Grant(input.Address, permission);

            return json;
        }

        private string ReadGrantFromBody(GrantPermissionInput input)
        {
            var permission = "";

            if (input.IsAdmin)
                permission += Permission.admin.ToString();

            if (input.IsReceive)
            {
                if (permission.Length > 0)
                    permission += ",";
                permission += Permission.receive.ToString();
            }

            if (input.IsSend)
            {
                if (permission.Length > 0)
                    permission += ",";
                permission += Permission.send.ToString();
            }

            if (input.IsConnect)
            {
                if (permission.Length > 0)
                    permission += ",";else
                permission += Permission.connect.ToString();
            }

            if (input.IsCreate)
            {
                if (permission.Length > 0)
                    permission += ",";
                permission += Permission.create.ToString();
            }

            if (input.IsIssue)
            {
                if (permission.Length > 0)
                    permission += ",";
                permission += Permission.issue.ToString();
            }

            if (input.IsMine)
            {
                if (permission.Length > 0)
                    permission += ",";
                
                permission += Permission.mine.ToString();
            }

            if (input.IsActivate)
            {
                if (permission.Length > 0)
                    permission += ",";
                permission += Permission.activate.ToString();
            }
            return permission;
        }

        public object IssueAsset(IssueAssetInput input)
        {
            if (!AuthAccountWithBearerToken(input.Address))
                return Properties.Resources.AccountNotFound;

            var json = assetManager.IssueAsset(input.Address, input.AssetName, input.Qty, input.Unit, input.Note);

            return json;
        }

        public object CreateTransaction(CreateTransactionInput input)
        {
            if (!AuthAccountWithBearerToken(input.AddressFrom))
                return Properties.Resources.AccountNotFound;

            var result = transactionManager.CreateRawSendFrom(
                addressFrom: input.AddressFrom, 
                addressTo: input.AddressTo, 
                assetname: input.AssetName, 
                qty: input.Qty
            );

            return result;
        }

        public object SignTransaction(SignTransactionInput input)
        {
            var t = database.Addresses.SingleOrDefault(address => address.email == account.email && address.addr == input.AddressSign);
            if (t == null)
                return Properties.Resources.AccountNotFound;

            var privateKey = t.privateKey;

            var result = transactionManager.SignRawTransaction(input.HexValue, privateKey);
            return result;
        }

        public object SendTransaction(SendTransactionInput input)
        {
            var result = transactionManager.SendRawTransaction(
                hex: input.HexValue
            );
            return result;
        }

        private bool AuthAccountWithBearerToken(string inputAddress)
        {
            var t = database.Addresses.SingleOrDefault(address => address.email == account.email && address.addr == inputAddress);
            if (t == null)
                return false;
            else
                return true;
        }
    }
}