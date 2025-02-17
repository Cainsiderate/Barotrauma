﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    partial class Voting
    {
        private struct SubmarineVoteInfo
        {
            public SubmarineInfo SubmarineInfo { get; set; }
            public bool TransferItems { get; set; }
            public int DeliveryFee { get; set; }

            public SubmarineVoteInfo(SubmarineInfo submarineInfo, bool transferItems, int deliveryFee)
            {
                SubmarineInfo = submarineInfo;
                TransferItems = transferItems;
                DeliveryFee = deliveryFee;
            }
        }

        private readonly Dictionary<VoteType, int>
            voteCountYes = new Dictionary<VoteType, int>(),
            voteCountNo = new Dictionary<VoteType, int>(),
            voteCountMax = new Dictionary<VoteType, int>();

        public int GetVoteCountYes(VoteType voteType)
        {
            voteCountYes.TryGetValue(voteType, out int value);
            return value;
        }
        public int GetVoteCountNo(VoteType voteType)
        {
            voteCountNo.TryGetValue(voteType, out int value);
            return value;
        }
        public int GetVoteCountMax(VoteType voteType)
        {
            voteCountMax.TryGetValue(voteType, out int value);
            return value;
        }
        public void SetVoteCountYes(VoteType voteType, int value)
        {
            voteCountYes[voteType] = value;
        }
        public void SetVoteCountNo(VoteType voteType, int value)
        {
            voteCountNo[voteType] = value;
        }
        public void SetVoteCountMax(VoteType voteType, int value)
        {
            voteCountMax[voteType] = value;
        }

        public void UpdateVoteTexts(List<Client> clients, VoteType voteType)
        {
            switch (voteType)
            {
                case VoteType.Sub:
                case VoteType.Mode:
                    GUIListBox listBox = (voteType == VoteType.Sub) ?
                        GameMain.NetLobbyScreen.SubList : GameMain.NetLobbyScreen.ModeList;

                    foreach (GUIComponent comp in listBox.Content.Children)
                    {
                        if (comp.FindChild("votes") is GUITextBlock voteText) { comp.RemoveChild(voteText); }
                    }

                    if (clients == null) { return; }
                    
                    IReadOnlyDictionary<object, int> voteList = GetVoteCounts<object>(voteType, clients);
                    foreach (KeyValuePair<object, int> votable in voteList)
                    {
                        SetVoteText(listBox, votable.Key, votable.Value);
                    }                    
                    break;
                case VoteType.StartRound:
                    if (clients == null) { return; }
                    foreach (Client client in clients)
                    {
                        var clientReady = GameMain.NetLobbyScreen.PlayerList.Content.FindChild(client)?.FindChild("clientready");
                        if (clientReady != null)
                        {
                            clientReady.Visible = client.GetVote<bool>(VoteType.StartRound);
                        }
                    }
                    break;
            }
        }

        private void SetVoteText(GUIListBox listBox, object userData, int votes)
        {
            if (userData == null) return;
            foreach (GUIComponent comp in listBox.Content.Children)
            {
                if (comp.UserData != userData) { continue; }
                if (!(comp.FindChild("votes") is GUITextBlock voteText))
                {
                    voteText = new GUITextBlock(new RectTransform(new Point(30, comp.Rect.Height), comp.RectTransform, Anchor.CenterRight),
                        "", textAlignment: Alignment.CenterRight)
                    {
                        Padding = Vector4.Zero,
                        UserData = "votes"
                    };
                }

                voteText.Text = votes == 0 ? "" : votes.ToString();
            }
        }

        public void ClientWrite(IWriteMessage msg, VoteType voteType, object data)
        {
            msg.Write((byte)voteType);

            switch (voteType)
            {
                case VoteType.Sub:
                    if (!(data is SubmarineInfo sub)) { return; }
                    msg.Write(sub.EqualityCheckVal);
                    if (sub.EqualityCheckVal == 0)
                    {
                        //sub doesn't exist client-side, use hash to let the server know which one we voted for
                        msg.Write(sub.MD5Hash.StringRepresentation);
                    }
                    break;
                case VoteType.Mode:
                    if (!(data is GameModePreset gameMode)) { return; }
                    msg.Write(gameMode.Identifier);
                    break;
                case VoteType.EndRound:
                    if (!(data is bool)) { return; }
                    msg.Write((bool)data);
                    break;
                case VoteType.Kick:
                    if (!(data is Client votedClient)) { return; }

                    msg.Write(votedClient.ID);
                    break;
                case VoteType.StartRound:
                    if (!(data is bool)) { return; }
                    msg.Write((bool)data);
                    break;
                case VoteType.PurchaseAndSwitchSub:
                case VoteType.PurchaseSub:
                case VoteType.SwitchSub:
                    if (data is (SubmarineInfo voteSub, bool transferItems))
                    { 
                        //initiate sub vote
                        msg.Write(true);
                        msg.Write(voteSub.Name);
                        msg.Write(transferItems);
                    }
                    else
                    {
                        // vote
                        if (!(data is int)) { return; }
                        msg.Write(false);
                        msg.Write((int)data);
                    }
                    break;
                case VoteType.TransferMoney:
                    if (!(data is int)) { return; }
                    msg.Write(false); //not initiating a vote
                    msg.Write((int)data);
                    break;
            }

            msg.WritePadBits();
        }
        
        public void ClientRead(IReadMessage inc)
        {
            GameMain.Client.ServerSettings.AllowSubVoting = inc.ReadBoolean();
            if (GameMain.Client.ServerSettings.AllowSubVoting)
            {
                UpdateVoteTexts(null, VoteType.Sub);
                int votableCount = inc.ReadByte();
                for (int i = 0; i < votableCount; i++)
                {
                    int votes = inc.ReadByte();
                    string subName = inc.ReadString();
                    List<SubmarineInfo> serversubs = new List<SubmarineInfo>();
                    if (GameMain.NetLobbyScreen?.SubList?.Content != null)
                    {
                        foreach (GUIComponent item in GameMain.NetLobbyScreen.SubList.Content.Children)
                        {
                            if (item.UserData != null && item.UserData is SubmarineInfo) { serversubs.Add(item.UserData as SubmarineInfo); }
                        }
                    }
                    SubmarineInfo sub = serversubs.FirstOrDefault(s => s.Name == subName);
                    SetVoteText(GameMain.NetLobbyScreen.SubList, sub, votes);
                }
            }
            GameMain.Client.ServerSettings.AllowModeVoting = inc.ReadBoolean();
            if (GameMain.Client.ServerSettings.AllowModeVoting)
            {
                UpdateVoteTexts(null, VoteType.Mode);
                int votableCount = inc.ReadByte();
                for (int i = 0; i < votableCount; i++)
                {
                    int votes = inc.ReadByte();
                    string modeIdentifier = inc.ReadString();
                    GameModePreset mode = GameModePreset.List.Find(m => m.Identifier == modeIdentifier);
                    SetVoteText(GameMain.NetLobbyScreen.ModeList, mode, votes);
                }
            }
            GameMain.Client.ServerSettings.AllowEndVoting = inc.ReadBoolean();
            if (GameMain.Client.ServerSettings.AllowEndVoting)
            {
                SetVoteCountYes(VoteType.EndRound, inc.ReadByte());
                SetVoteCountMax(VoteType.EndRound, inc.ReadByte());
            }
            GameMain.Client.ServerSettings.AllowVoteKick = inc.ReadBoolean();

            byte activeVoteStateByte = inc.ReadByte();

            VoteState activeVoteState = VoteState.None;
            try { activeVoteState = (VoteState)activeVoteStateByte; }
            catch (System.Exception e)
            {
                DebugConsole.ThrowError("Failed to cast vote type \"" + activeVoteStateByte + "\"", e);
            }

            if (activeVoteState != VoteState.None)
            {
                byte voteTypeByte = inc.ReadByte();
                VoteType voteType = VoteType.Unknown;
                try { voteType = (VoteType)voteTypeByte; }
                catch (System.Exception e)
                {
                    DebugConsole.ThrowError("Failed to cast vote type \"" + voteTypeByte + "\"", e);
                }

                byte yesClientCount = inc.ReadByte();
                for (int i = 0; i < yesClientCount; i++)
                {
                    byte clientID = inc.ReadByte();
                    var matchingClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == clientID);
                    matchingClient?.SetVote(voteType, 2);
                }

                byte noClientCount = inc.ReadByte();
                for (int i = 0; i < noClientCount; i++)
                {
                    byte clientID = inc.ReadByte();
                    var matchingClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == clientID);
                    matchingClient?.SetVote(voteType, 1);
                }
                byte maxClientCount = inc.ReadByte();

                SetVoteCountYes(voteType, yesClientCount);
                SetVoteCountNo(voteType, noClientCount);
                SetVoteCountMax(voteType, maxClientCount);

                switch (activeVoteState)
                {
                    case VoteState.Started:
                        byte starterID = inc.ReadByte();
                        Client starterClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == starterID);
                        float timeOut = inc.ReadByte();

                        Client myClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == GameMain.Client.ID);
                        if (myClient == null || !myClient.InGame)  { return; }

                        switch (voteType)
                        {
                            case VoteType.PurchaseSub:
                            case VoteType.PurchaseAndSwitchSub:
                            case VoteType.SwitchSub:
                                string subName1 = inc.ReadString();
                                bool transferItems = inc.ReadBoolean();
                                SubmarineInfo info = GameMain.Client.ServerSubmarines.FirstOrDefault(s => s.Name == subName1);
                                if (info == null)
                                {
                                    DebugConsole.ThrowError("Failed to find a matching submarine, vote aborted");
                                    return;
                                }
                                GameMain.Client.ShowSubmarineChangeVoteInterface(starterClient, info, voteType, transferItems, timeOut);
                                break;
                            case VoteType.TransferMoney:
                                byte fromClientId = inc.ReadByte();
                                byte toClientId = inc.ReadByte();
                                int transferAmount = inc.ReadInt32();

                                Client fromClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == fromClientId);
                                Client toClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == toClientId);
                                GameMain.Client.ShowMoneyTransferVoteInterface(starterClient, fromClient, transferAmount, toClient, timeOut);
                                break;
                        }
                        break;
                    case VoteState.Running:
                        // Nothing specific
                        break;
                    case VoteState.Passed:
                    case VoteState.Failed:
                        bool passed = inc.ReadBoolean();
                        SubmarineVoteInfo submarineVoteInfo = default;
                        switch (voteType)
                        {
                            case VoteType.PurchaseSub:
                            case VoteType.PurchaseAndSwitchSub:
                            case VoteType.SwitchSub:
                                string subName2 = inc.ReadString();
                                var submarineInfo = GameMain.Client.ServerSubmarines.FirstOrDefault(s => s.Name == subName2);
                                bool transferItems = inc.ReadBoolean();
                                int deliveryFee = inc.ReadInt16();
                                if (submarineInfo == null)
                                {
                                    DebugConsole.ThrowError("Failed to find a matching submarine, vote aborted");
                                    return;
                                }
                                submarineVoteInfo = new SubmarineVoteInfo(submarineInfo, transferItems, deliveryFee);
                                break;
                        }

                        GameMain.Client.VotingInterface?.EndVote(passed, yesClientCount, noClientCount);                        

                        if (passed && submarineVoteInfo.SubmarineInfo is { } subInfo)
                        {
                            switch (voteType)
                            {
                                case VoteType.PurchaseAndSwitchSub:
                                    GameMain.GameSession.PurchaseSubmarine(subInfo);
                                    GameMain.GameSession.SwitchSubmarine(subInfo, submarineVoteInfo.TransferItems, 0);
                                    break;
                                case VoteType.PurchaseSub:
                                    GameMain.GameSession.PurchaseSubmarine(subInfo);
                                    break;
                                case VoteType.SwitchSub:
                                    GameMain.GameSession.SwitchSubmarine(subInfo, submarineVoteInfo.TransferItems, submarineVoteInfo.DeliveryFee);
                                    break;
                            }

                            SubmarineSelection.ContentRefreshRequired = true;
                        }
                        break;
                }
            }

            GameMain.NetworkMember.ConnectedClients.ForEach(c => c.SetVote(VoteType.StartRound, false));
            byte readyClientCount = inc.ReadByte();
            for (int i = 0; i < readyClientCount; i++)
            {
                byte clientID = inc.ReadByte();
                var matchingClient = GameMain.NetworkMember.ConnectedClients.Find(c => c.ID == clientID);
                matchingClient?.SetVote(VoteType.StartRound, true);
            }
            UpdateVoteTexts(GameMain.NetworkMember.ConnectedClients, VoteType.StartRound);

            inc.ReadPadBits();
        }
    }
}
