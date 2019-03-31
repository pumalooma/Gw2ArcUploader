﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace EVTC_Log_Parser.Model
{
    public class Parser
    {
	    private BinaryReader _reader;

	    public Metadata Metadata { get; set; }
        public List<Player> Players { get; set; }
        public List<NPC> NPCs { get; set; }
        public List<Gadget> Gadgets { get; set; }
        public List<Skill> Skills { get; set; }
        public List<Event> Events { get; set; }

	    public bool Parse(string filePath)
        {
            try
            {
                if (Path.GetExtension(filePath) == ".evtc")
                {
                    _reader = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read));
                }
                else
                {
                    Stream stream = ZipFile.OpenRead(filePath).Entries[0].Open();
                    MemoryStream memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;
                    _reader = new BinaryReader(memStream);
                }
                using (_reader)
                {
                    Metadata = new Metadata();
                    Players = new List<Player>();
                    NPCs = new List<NPC>();
                    Gadgets = new List<Gadget>();
                    Skills = new List<Skill>();
                    Events = new List<Event>();
                    ParseMetadata();
                    if (int.Parse(Metadata.ArcVersion.Substring(4)) < 20170218) return false;
                    ParseAgents();
                    ParseSkills();
                    ParseEvents();
                    FillMissingData();
                    return true;
                }
            }
            catch (Exception e)
            {
                var msg = e.ToString();
                return false;
            }
        }

	    private void ParseMetadata()
        {
            Metadata.ArcVersion = _reader.ReadUTF8(12); // 12 bytes: build version 
            Metadata.TargetSpeciesId = _reader.Skip(1).ReadUInt16(); // 2 bytes: instid
        }

        private void ParseAgents()
        {
            // 4 bytes: agent count
            int ac = _reader.Skip(1).ReadInt32();

            // 96 bytes: each agent
            for (int i = 0; i < ac; i++)
            {
                // add agent
                AddAgent();
            }
        }

        private void ParseSkills()
        {
            // 4 bytes: skill count
            int sc = _reader.ReadInt32();

            // 68 bytes: each skill
            for (int i = 0; i < sc; i++)
            {
                Skills.Add(new Skill()
                {
                    Id = _reader.ReadInt32(), // 4 bytes: id
                    Name = _reader.ReadUTF8(64) // 64 bytes: name
                });
            }
        }

        private void ParseEvents()
        {
            // Until EOF
            while (_reader.BaseStream.Position != _reader.BaseStream.Length)
            {
                Event combat = new Event();
	            combat.Time = (int)_reader.ReadUInt64();
	            combat.SrcAgent = _reader.ReadUInt64Hex();
	            combat.DstAgent = _reader.ReadUInt64Hex();
	            combat.Value = _reader.ReadInt32();
	            combat.BuffDmg = _reader.ReadInt32();
	            combat.OverstackValue = _reader.ReadUInt16();
	            combat.SkillId = _reader.ReadUInt16();
	            combat.SrcInstid = _reader.ReadUInt16();
	            combat.DstInstid = _reader.ReadUInt16();
	            combat.SrcMasterInstid = _reader.ReadUInt16();
				combat.IFF = (IFF)_reader.Skip(9).ReadByte();
	            combat.IsBuff = _reader.ReadBoolean();
	            combat.Result = (Result)_reader.Read();
	            combat.Activation = (Activation)_reader.Read();
	            combat.BuffRemove = (BuffRemove)_reader.Read();
	            combat.IsNinety = _reader.ReadBoolean();
	            combat.IsFifty = _reader.ReadBoolean();
	            combat.IsMoving = _reader.ReadBoolean();
	            combat.StateChange = (StateChange)_reader.Read();
	            combat.IsFlanking = _reader.ReadBoolean();
	            combat.IsShield = _reader.ReadBoolean();

	            // Add Combat
                _reader.Skip(2);
                Events.Add(combat);
            }
        }

        private Profession ParseProfession(int pLower, int pUpper, int isElite)
        {
            if (isElite == -1)
            {
                return (pUpper == 65535) ? Profession.Gadget : Profession.NPC;
            }
            else
            {
                if (int.Parse(Metadata.ArcVersion.Substring(4)) < 20170905)
                {
                    return (Profession)pLower + (9 * isElite);
                }
                else
                {
                    return (isElite == 0) ? (Profession)pLower : ((Specialization)isElite).ToProfession();
                }
            }
        }

        private void AddAgent()
        {
            string address = _reader.ReadUInt64Hex(); // 8 bytes: Address
            int pLower = BitConverter.ToUInt16(_reader.ReadBytes(2), 0); // 2 bytes: Prof (lower bytes)
            int pUpper = BitConverter.ToUInt16(_reader.ReadBytes(2), 0); // 2 bytes: prof (upper bytes)
            int isElite = _reader.ReadInt32(); // 4 bytes: IsElite
            int toughness = _reader.ReadInt32();  // 4 bytes: Toughness
            int healing = _reader.ReadInt32();  // 4 bytes: Healing
            int condition = _reader.ReadInt32();  // 4 bytes: Condition
            string name = _reader.ReadUTF8(68); // 68 bytes: Name

            // Add Agent by Type
            Profession profession = ParseProfession(pLower, pUpper, isElite);
            switch (profession)
            {
                case Profession.Gadget:
                    Gadgets.Add(new Gadget()
                    {
                        Address = address,
                        PseudoId = pLower,
                        Name = name
                    });
                    return;
                case Profession.NPC:
                    NPCs.Add(new NPC()
                    {
                        Address = address,
                        SpeciesId = pLower,
                        Toughness = toughness,
                        Healing = healing,
                        Condition = condition,
                        Name = name
                    });
                    return;
                default:
                    Players.Add(new Player(name)
                    {
                        Address = address,
                        Profession = profession,
                        Toughness = toughness,
                        Healing = healing,
                        Condition = condition,
                    });
                    return;
            }
        }

        private void FillMissingData()
        {
            // Update Instid
            foreach (Player p in Players)
            {
                foreach (Event e in Events)
                {
                    if (p.Address == e.SrcAgent && e.SrcInstid != 0)
                    {
                        p.Instid = e.SrcInstid;
                    }
                    else if (p.Address == e.DstAgent && e.DstInstid != 0)
                    {
                        p.Instid = e.DstInstid;
                    }
                }
            }
            foreach (NPC n in NPCs)
            {
                foreach (Event e in Events)
                {
                    if (n.Address == e.SrcAgent && e.SrcInstid != 0)
                    {
                        n.Instid = e.SrcInstid;
                    }
                    else if (n.Address == e.DstAgent && e.DstInstid != 0)
                    {

                        n.Instid = e.DstInstid;
                    }
                }
            }

            // Update Metadata
            IEnumerable<Event> sc = Events.Where(e => e.StateChange != StateChange.None);
            foreach (Event e in sc)
            {
                StateChange s = e.StateChange;
                if (s == StateChange.LogStart)
                {
                    Metadata.LogStart = DateTimeOffset.FromUnixTimeSeconds(e.Value).DateTime;
                }
                else if (s == StateChange.LogEnd)
                {
                    Metadata.LogEnd = DateTimeOffset.FromUnixTimeSeconds(e.Value).DateTime;
                }
                else if (s == StateChange.PointOfView)
                {
                    Metadata.PointOfView = (Players.Find(p => p.Address == e.SrcAgent) != null) ? Players.Find(p => p.Address == e.SrcAgent).Account : ":?.????";
                }
                else if (s == StateChange.Language)
                {
                    Metadata.Language = (Language)e.Value;
                }
                else if (s == StateChange.GWBuild)
                {
                    Metadata.GWBuild = int.Parse(e.SrcAgent, NumberStyles.HexNumber);
                }
                else if (s == StateChange.ShardID)
                {
                    Metadata.ShardID = int.Parse(e.SrcAgent, NumberStyles.HexNumber);
                }
            }

            // Normallize Time
            int ts = Events[0].Time;
            Events.ForEach(e => e.Time -= ts);

            // Target
            NPC tg = NPCs.Find(n => n.SpeciesId == Metadata.TargetSpeciesId);

            // Adjust Xera Instids
            if (tg.SpeciesId == 16246)
            {
                NPC shx = NPCs.Find(n => n.SpeciesId == 16286);
                if (shx != null)
                {
                    foreach (Event e in Events)
                    {
                        if (e.SrcInstid == shx.Instid)
                        {
                            e.SrcInstid = tg.Instid;
                        }
                        else if (e.DstInstid == shx.Instid)
                        {
                            e.DstInstid = tg.Instid;
                        }
                        else if (e.SrcMasterInstid == shx.Instid)
                        {
                            e.SrcMasterInstid = tg.Instid;
                        }
                    }
                }
            }

            // Set Aware Times for Target
            tg.FirstAware = Events.Where(e => e.SrcInstid == tg.Instid).First().Time;

            var events = Events.Where(e => e.SrcInstid == tg.Instid);
            var enumerable = events.Where(e => e.StateChange != StateChange.None);

            Event tde = Events.Find(e => e.StateChange == StateChange.ChangeDead && e.SrcInstid == tg.Instid);
            if (tde != null)
            {
                tg.LastAware = tde.Time;
                Events = Events.TakeWhile(e => !(e.StateChange == StateChange.ChangeDead && e.SrcInstid == tg.Instid)).ToList(); // Trim Events After Death
                tg.Died = true;
            }
            else
            {
                tg.LastAware = Events.Where(e => e.SrcInstid == tg.Instid).Last().Time;
                tg.Died = false;
            }
        }
    }
}
