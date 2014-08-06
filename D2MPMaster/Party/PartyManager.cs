// 
// PartyManager.cs
// Created by ilian000 on 2014-08-06
// Licenced under the Apache License, Version 2.0
//

using D2MPMaster.Browser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using D2MPMaster.LiveData;
using Newtonsoft.Json;

namespace D2MPMaster.Party
{
    public class PartyManager : XSocketController, IDisposable
    {
        private static readonly BrowserController Browsers = new BrowserController();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static void JoinParty(BrowserController c, JObject jdata, string steamid)
        {
            var party = Browsers.Find(m => m.user != null && m.party != null && m.user.steam.steamid == steamid).FirstOrDefault().party;
            if (party == null)
            {
                c.RespondError(jdata, "Can't find that party.");
                return;
            }
            if (party.invitedUsers.Contains(c.user.steam.steamid))
            {
                if (party.users.Count() < party.users.Capacity)
                {
                    lock (party.users)
                        party.users.Add(PartyMember.FromUser(c.user));
                    c.party = party;
                }
                else
                {
                    c.RespondError(jdata, "Party is full");
                    return;
                }
            }
            else
            {
                c.RespondError(jdata, "You are not invited to that party.");
                return;
            }

            TransmitPartyUpdate(party, new[] { "users" });
        }

        public static void LeaveParty(BrowserController c)
        {
            if (c.party == null || c.user == null) return;
            var party = c.party;
            c.party = null;
            if (party.users.Where(x => x.id == c.user.steam.steamid).Any())
                lock (party.users)
                    party.users.RemoveAll(x => x.id == c.user.steam.steamid);
            TransmitPartyUpdate(party, new[] { "users" });
        }

        public static void TransmitPartyUpdate(Party party, string[] fields)
        {
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { party.Update("parties", fields) };
            Browsers.AsyncSendTo(m => m.party != null && m.party.id == party.id, new TextArgs(upd.ToString(Formatting.None), "party"),
                req => { });
        }

        public static void InviteFriend(BrowserController c, string steamid)
        {
            if (c.party == null)
            {
                // Let's create a party
                c.party = new Party()
                {
                    id = Utils.RandomString(17),
                    creatorid = c.user.Id,
                    users = new List<PartyMember>(5) { PartyMember.FromUser(c.user) },
                    invitedUsers = new List<string>() { steamid }
                };
            }
            if (!c.party.invitedUsers.Contains(steamid))
                c.party.invitedUsers.Add(steamid);
            Browsers.AsyncSendTo(m => m.user != null && m.user.steam.steamid == steamid, BrowserController.inviteFriendParty(c.lobby, c.user.steam.steamid), req => { });
        }
        public void Dispose()
        {
        }
    }
}
