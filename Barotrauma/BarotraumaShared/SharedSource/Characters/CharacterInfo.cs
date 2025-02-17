using Barotrauma.Extensions;
using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Barotrauma.IO;
using System.Linq;
using System.Xml.Linq;
using Barotrauma.Abilities;

namespace Barotrauma
{
    class CharacterInfoPrefab
    {
        public readonly ImmutableArray<CharacterInfo.HeadPreset> Heads;
        public readonly ImmutableDictionary<Identifier, ImmutableHashSet<Identifier>> VarTags;
        public readonly Identifier MenuCategoryVar;
        public readonly Identifier Pronouns;

        public CharacterInfoPrefab(ContentXElement headsElement, XElement varsElement, XElement menuCategoryElement, XElement pronounsElement)
        {
            Heads = headsElement.Elements().Select(e => new CharacterInfo.HeadPreset(this, e)).ToImmutableArray();
            if (varsElement != null)
            {
                VarTags = varsElement.Elements()
                    .Select(e =>
                        (e.GetAttributeIdentifier("var", ""),
                            e.GetAttributeIdentifierArray("tags", Array.Empty<Identifier>()).ToImmutableHashSet()))
                    .ToImmutableDictionary();
            }
            else
            {
                VarTags = new[]
                {
                    ("GENDER".ToIdentifier(),
                        new[] { "female".ToIdentifier(), "male".ToIdentifier() }.ToImmutableHashSet())
                }.ToImmutableDictionary();
            }

            MenuCategoryVar = menuCategoryElement?.GetAttributeIdentifier("var", Identifier.Empty) ?? "GENDER".ToIdentifier();
            Pronouns = pronounsElement?.GetAttributeIdentifier("vars", Identifier.Empty) ?? "GENDER".ToIdentifier();
        }
        public string ReplaceVars(string str, CharacterInfo.HeadPreset headPreset)
        {
            return ReplaceVars(str, headPreset.TagSet);
        }

        public string ReplaceVars(string str, ImmutableHashSet<Identifier> tagSet)
        {
            foreach (var key in VarTags.Keys)
            {
                str = str.Replace($"[{key}]", tagSet.FirstOrDefault(t => VarTags[key].Contains(t)).Value, StringComparison.OrdinalIgnoreCase);
            }
            return str;
        }
    }

    partial class CharacterInfo
    {
        public class HeadInfo
        {
            public readonly CharacterInfo CharacterInfo;
            public readonly HeadPreset Preset;

            private int hairIndex;

            public int HairIndex
            {
                get => hairIndex;
                set
                {
                    hairIndex = value;
                    if (CharacterInfo.Hairs is null)
                    {
                        HairWithHatIndex = value;
                        return;
                    }
                    HairWithHatIndex = HairElement?.GetAttributeInt("replacewhenwearinghat", hairIndex) ?? -1;
                    if (HairWithHatIndex < 0 || HairWithHatIndex >= CharacterInfo.Hairs.Count)
                    {
                        HairWithHatIndex = hairIndex;
                    }
                }
            }
            public int HairWithHatIndex { get; private set; }
            public int BeardIndex;
            public int MoustacheIndex;
            public int FaceAttachmentIndex;

            public Color HairColor;
            public Color FacialHairColor;
            public Color SkinColor;

            public Vector2 SheetIndex => Preset.SheetIndex;

            public ContentXElement HairElement
            {
                get
                {
                    if (CharacterInfo.Hairs == null) { return null; }
                    if (hairIndex >= CharacterInfo.Hairs.Count)
                    {
                        DebugConsole.AddWarning($"Hair index out of range (character: {CharacterInfo?.Name ?? "null"}, index: {hairIndex})");
                    }
                    return CharacterInfo.Hairs.ElementAtOrDefault(hairIndex);
                }
            }
            public ContentXElement HairWithHatElement
            {
                get
                {
                    if (CharacterInfo.Hairs == null) { return null; }
                    if (HairWithHatIndex >= CharacterInfo.Hairs.Count)
                    {
                        DebugConsole.AddWarning($"Hair with hat index out of range (character: {CharacterInfo?.Name ?? "null"}, index: {HairWithHatIndex})");
                    }
                    return CharacterInfo.Hairs.ElementAtOrDefault(HairWithHatIndex);
                }
            }            

            public ContentXElement BeardElement
            {
                get
                {
                    if (CharacterInfo.Beards == null) { return null; }
                    if (BeardIndex >= CharacterInfo.Beards.Count)
                    {
                        DebugConsole.AddWarning($"Beard index out of range (character: {CharacterInfo?.Name ?? "null"}, index: {BeardIndex})");
                    }
                    return CharacterInfo.Beards.ElementAtOrDefault(BeardIndex);
                }
            }
            public ContentXElement MoustacheElement
            {
                get
                {
                    if (CharacterInfo.Moustaches == null) { return null; }
                    if (MoustacheIndex >= CharacterInfo.Moustaches.Count)
                    {
                        DebugConsole.AddWarning($"Moustache index out of range (character: {CharacterInfo?.Name ?? "null"}, index: {MoustacheIndex})");
                    }
                    return CharacterInfo.Moustaches.ElementAtOrDefault(MoustacheIndex);
                }
            }
            public ContentXElement FaceAttachment
            {
                get
                {
                    if (CharacterInfo.FaceAttachments == null) { return null; }
                    if (FaceAttachmentIndex >= CharacterInfo.FaceAttachments.Count)
                    {
                        DebugConsole.AddWarning($"Face attachment index out of range (character: {CharacterInfo?.Name ?? "null"}, index: {FaceAttachmentIndex})");
                    }
                    return CharacterInfo.FaceAttachments.ElementAtOrDefault(FaceAttachmentIndex);
                }
            }

            public HeadInfo(CharacterInfo characterInfo, HeadPreset headPreset, int hairIndex = 0, int beardIndex = 0, int moustacheIndex = 0, int faceAttachmentIndex = 0)
            {
                CharacterInfo = characterInfo;
                Preset = headPreset;
                HairIndex = hairIndex;
                BeardIndex = beardIndex;
                MoustacheIndex = moustacheIndex;
                FaceAttachmentIndex = faceAttachmentIndex;
            }

            public void ResetAttachmentIndices()
            {
                HairIndex = -1;
                BeardIndex = -1;
                MoustacheIndex = -1;
                FaceAttachmentIndex = -1;
            }
        }

        private HeadInfo head;
        public HeadInfo Head
        {
            get { return head; }
            set
            {
                if (head != value && value != null)
                {
                    head = value;
                    HeadSprite = null;
                    AttachmentSprites = null;
                    hairs = null;
                    beards = null;
                    moustaches = null;
                    faceAttachments = null;
                }
            }
        }

        private readonly Identifier maleIdentifier = "Male".ToIdentifier();
        private readonly Identifier femaleIdentifier = "Female".ToIdentifier();

        public bool IsMale { get { return head?.Preset?.TagSet?.Contains(maleIdentifier) ?? false; } }
        public bool IsFemale { get { return head?.Preset?.TagSet?.Contains(femaleIdentifier) ?? false; } }

        public CharacterInfoPrefab Prefab => CharacterPrefab.Prefabs[SpeciesName].CharacterInfoPrefab;
        public class HeadPreset : ISerializableEntity
        {
            private readonly CharacterInfoPrefab characterInfoPrefab;
            public Identifier MenuCategory => TagSet.First(t => characterInfoPrefab.VarTags[characterInfoPrefab.MenuCategoryVar].Contains(t));

            public ImmutableHashSet<Identifier> TagSet { get; private set; }

            [Serialize("", IsPropertySaveable.No)]
            public string Tags
            {
                get { return string.Join(",", TagSet); }
                private set
                {
                    TagSet = value.Split(",")
                        .Select(s => s.ToIdentifier())
                        .Where(id => !id.IsEmpty)
                        .ToImmutableHashSet();
                }
            }

            [Serialize("0,0", IsPropertySaveable.No)]
            public Vector2 SheetIndex { get; private set; }

            public string Name => $"Head Preset {Tags}";

            public Dictionary<Identifier, SerializableProperty> SerializableProperties { get; private set; }

            public HeadPreset(CharacterInfoPrefab charInfoPrefab, XElement element)
            {
                characterInfoPrefab = charInfoPrefab;
                SerializableProperties = SerializableProperty.DeserializeProperties(this, element);
                DetermineTagsFromLegacyFormat(element);
            }

            private void DetermineTagsFromLegacyFormat(XElement element)
            {
                void addTag(string tag)
                    => TagSet = TagSet.Add(tag.ToIdentifier());
                
                string headId = element.GetAttributeString("id", "");
                string gender = element.GetAttributeString("gender", "");
                string race = element.GetAttributeString("race", "");
                if (!headId.IsNullOrEmpty()) { addTag($"head{headId}"); }
                if (!gender.IsNullOrEmpty()) { addTag(gender); }
                if (!race.IsNullOrEmpty()) { addTag(race); }
            }
        }

        public XElement InventoryData;
        public XElement HealthData;
        public XElement OrderData;

        private static ushort idCounter;
        private const string disguiseName = "???";

        public bool HasNickname => Name != OriginalName;
        public string OriginalName { get; private set; }

        public string Name;

        public string DisplayName
        {
            get
            {
                if (Character == null || !Character.HideFace)
                {
                    IsDisguised = IsDisguisedAsAnother = false;
                    return Name;
                }
                else if ((GameMain.NetworkMember != null && !GameMain.NetworkMember.ServerSettings.AllowDisguises))
                {
                    IsDisguised = IsDisguisedAsAnother = false;
                    return Name;
                }

                if (Character.Inventory != null)
                {
                    //Disguise as the ID card name if it's equipped      
                    var idCard = Character.Inventory.GetItemInLimbSlot(InvSlotType.Card);
                    return idCard?.GetComponent<IdCard>()?.OwnerName ?? disguiseName;
                }
                return disguiseName;
            }
        }

        public Identifier SpeciesName { get; }

        /// <summary>
        /// Note: Can be null.
        /// </summary>
        public Character Character;
        
        public Job Job;
        
        public int Salary;

        public int ExperiencePoints { get; private set; }

        public HashSet<Identifier> UnlockedTalents { get; private set; } = new HashSet<Identifier>();

        /// <summary>
        /// Endocrine boosters can unlock talents outside the user's talent tree. This method is used to cull them from the selection
        /// </summary>
        public IEnumerable<Identifier> GetUnlockedTalentsInTree()
        {
            if (!TalentTree.JobTalentTrees.TryGet(Job.Prefab.Identifier, out TalentTree talentTree)) { return Enumerable.Empty<Identifier>(); }

            return UnlockedTalents.Where(t => talentTree.TalentIsInTree(t));
        }

        /// <summary>
        /// Returns unlocked talents that aren't part of the character's talent tree (which can be unlocked e.g. with an endocrine booster)
        /// </summary>
        public IEnumerable<Identifier> GetUnlockedTalentsOutsideTree()
        {
            if (!TalentTree.JobTalentTrees.TryGet(Job.Prefab.Identifier, out TalentTree talentTree)) { return Enumerable.Empty<Identifier>(); }
            return UnlockedTalents.Where(t => !talentTree.TalentIsInTree(t));
        }

        public const int MaxAdditionalTalentPoints = 100;

        private int additionalTalentPoints;
        public int AdditionalTalentPoints 
        {
            get { return additionalTalentPoints; }
            set { additionalTalentPoints = MathHelper.Clamp(value, 0, MaxAdditionalTalentPoints); }
        }

        private Sprite _headSprite;
        public Sprite HeadSprite
        {
            get
            {
                if (_headSprite == null)
                {
                    LoadHeadSprite();
                }
#if CLIENT
                if (_headSprite != null)
                {
                    CalculateHeadPosition(_headSprite);
                }
#endif
                return _headSprite;
            }
            private set
            {
                if (_headSprite != null)
                {
                    _headSprite.Remove();
                }
                _headSprite = value;
            }
        }

        /// <summary>
        /// Can be used to disable displaying the job in any info panels
        /// </summary>
        public bool OmitJobInMenus;

        private Sprite portrait;
        public Sprite Portrait
        {
            get
            {
                if (portrait == null)
                {
                    LoadHeadSprite();
                }
                return portrait;
            }
            private set
            {
                if (portrait != null)
                {
                    portrait.Remove();
                }
                portrait = value;
            }
        }

        public bool IsDisguised = false;
        public bool IsDisguisedAsAnother = false;

        public void CheckDisguiseStatus(bool handleBuff, IdCard idCard = null)
        {
            if (Character == null) { return; }

            string currentlyDisplayedName = DisplayName;

            IsDisguised = currentlyDisplayedName == disguiseName;
            IsDisguisedAsAnother = !IsDisguised && currentlyDisplayedName != Name;

            if (IsDisguisedAsAnother)
            {
                if (handleBuff)
                {
                    var head = Character.AnimController.GetLimb(LimbType.Head);
                    if (head != null)
                    {
                        Character.CharacterHealth.ApplyAffliction(head, AfflictionPrefab.List.FirstOrDefault(a => a.Identifier == "disguised").Instantiate(100f));
                    }
                }

                idCard ??= Character.Inventory?.GetItemInLimbSlot(InvSlotType.Card)?.GetComponent<IdCard>();
                if (idCard != null)
                {
#if CLIENT
                    GetDisguisedSprites(idCard);
#endif
                    return;
                }
            }

#if CLIENT
            disguisedJobIcon = null;
            disguisedPortrait = null;
#endif

            if (handleBuff)
            {
                var head = Character.AnimController.GetLimb(LimbType.Head);
                if (head != null)
                {
                    Character.CharacterHealth.ReduceAfflictionOnLimb(head, "disguised".ToIdentifier(), 100f);
                }
            }
        }

        private List<WearableSprite> attachmentSprites;
        public List<WearableSprite> AttachmentSprites
        {
            get
            {
                if (attachmentSprites == null)
                {
                    LoadAttachmentSprites();
                }
                return attachmentSprites;
            }
            private set
            {
                if (attachmentSprites != null)
                {
                    attachmentSprites.ForEach(s => s.Sprite?.Remove());
                }
                attachmentSprites = value;
            }
        }

        public ContentXElement CharacterConfigElement { get; set; }

        public readonly string ragdollFileName = string.Empty;

        public bool StartItemsGiven;

        public bool IsNewHire;

        public CauseOfDeath CauseOfDeath;

        public CharacterTeamType TeamID;

        public NPCPersonalityTrait PersonalityTrait { get; private set; }

        public const int MaxCurrentOrders = 3;
        public static int HighestManualOrderPriority => MaxCurrentOrders;

        public int GetManualOrderPriority(Order order)
        {
            if (order != null && order.AssignmentPriority < 100 && CurrentOrders.Any())
            {
                int orderPriority = HighestManualOrderPriority;
                for (int i = 0; i < CurrentOrders.Count; i++)
                {
                    if (order.AssignmentPriority >= CurrentOrders[i].AssignmentPriority)
                    {
                        break;
                    }
                    else
                    {
                        orderPriority--;
                    }
                }
                return Math.Max(orderPriority, 1);
            }
            else
            {
                return HighestManualOrderPriority;
            }
        }

        public List<Order> CurrentOrders { get; } = new List<Order>();

        //unique ID given to character infos in MP
        //used by clients to identify which infos are the same to prevent duplicate characters in round summary
        public ushort ID;

        public List<Identifier> SpriteTags
        {
            get;
            private set;
        }

        public readonly bool HasSpecifierTags;

        private RagdollParams ragdoll;
        public RagdollParams Ragdoll
        {
            get
            {
                if (ragdoll == null)
                {
                    // TODO: support for variants
                    Identifier speciesName = SpeciesName;
                    bool isHumanoid = CharacterConfigElement.GetAttributeBool("humanoid", speciesName == CharacterPrefab.HumanSpeciesName);
                    ragdoll = isHumanoid 
                        ? HumanRagdollParams.GetRagdollParams(speciesName, ragdollFileName)
                        : RagdollParams.GetRagdollParams<FishRagdollParams>(speciesName, ragdollFileName) as RagdollParams;
                }
                return ragdoll;
            }
            set { ragdoll = value; }
        }

        public bool IsAttachmentsLoaded => Head.HairIndex > -1 && Head.BeardIndex > -1 && Head.MoustacheIndex > -1 && Head.FaceAttachmentIndex > -1;

        public IEnumerable<ContentXElement> GetValidAttachmentElements(IEnumerable<ContentXElement> elements, HeadPreset headPreset, WearableType? wearableType = null)
            => FilterElements(elements, headPreset.TagSet, wearableType);
        
        public int CountValidAttachmentsOfType(WearableType wearableType)
            => GetValidAttachmentElements(Wearables, Head.Preset, wearableType).Count();

        public readonly ImmutableArray<(Color Color, float Commonness)> HairColors;
        public readonly ImmutableArray<(Color Color, float Commonness)> FacialHairColors;
        public readonly ImmutableArray<(Color Color, float Commonness)> SkinColors;
        
        private void GetName(Rand.RandSync randSync, out string name)
        {
            var nameElement = CharacterConfigElement.GetChildElement("names") ?? CharacterConfigElement.GetChildElement("name");
            ContentPath namesXmlFile = nameElement?.GetAttributeContentPath("path") ?? ContentPath.Empty;
            XElement namesXml = null;
            if (!namesXmlFile.IsNullOrEmpty()) //names.xml is defined 
            {
                XDocument doc = XMLExtensions.TryLoadXml(namesXmlFile);
                namesXml = doc.Root;
            }
            else //the legacy firstnames.txt/lastnames.txt shit is defined
            {
                namesXml = new XElement("names", new XAttribute("format", "[firstname] [lastname]"));
                var firstNamesPath = ReplaceVars(nameElement.GetAttributeContentPath("firstname")?.Value ?? "");
                var lastNamesPath = ReplaceVars(nameElement.GetAttributeContentPath("lastname")?.Value ?? "");
                if (File.Exists(firstNamesPath) && File.Exists(lastNamesPath))
                {
                    var firstNames = File.ReadAllLines(firstNamesPath);
                    var lastNames = File.ReadAllLines(lastNamesPath);
                    namesXml.Add(firstNames.Select(n => new XElement("firstname", new XAttribute("value", n))));
                    namesXml.Add(lastNames.Select(n => new XElement("lastname", new XAttribute("value", n))));
                }
                else //the files don't exist, just fall back to the vanilla names
                {
                    XDocument doc = XMLExtensions.TryLoadXml("Content/Characters/Human/names.xml");
                    namesXml = doc.Root;
                }
            }
            name = namesXml.GetAttributeString("format", "");
            Dictionary<Identifier, List<string>> entries = new Dictionary<Identifier, List<string>>();
            foreach (var subElement in namesXml.Elements())
            {
                Identifier elemName = subElement.NameAsIdentifier();
                if (!entries.ContainsKey(elemName))
                {
                    entries.Add(elemName, new List<string>());
                }
                ImmutableHashSet<Identifier> identifiers = subElement.GetAttributeIdentifierArray("tags", Array.Empty<Identifier>()).ToImmutableHashSet();
                if (identifiers.IsSubsetOf(Head.Preset.TagSet))
                {
                    entries[elemName].Add(subElement.GetAttributeString("value", ""));
                }
            }

            foreach (var k in entries.Keys)
            {
                name = name.Replace($"[{k}]", entries[k].GetRandom(randSync), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void LoadTagsBackwardsCompatibility(XElement element, HashSet<Identifier> tags)
        {
            //we need this to be able to load save files from
            //older versions with the shittier hardcoded character
            //info implementation
            Identifier gender = element.GetAttributeIdentifier("gender", "");
            int headSpriteId = element.GetAttributeInt("headspriteid", -1);
            if (!gender.IsEmpty) { tags.Add(gender); }
            if (headSpriteId > 0) { tags.Add($"head{headSpriteId}".ToIdentifier()); }
        }

        // talent-relevant values
        public int MissionsCompletedSinceDeath = 0;

        private static bool ElementHasSpecifierTags(XElement element)
            => element.GetAttributeBool("specifiertags",
                element.GetAttributeBool("genders",
                    element.GetAttributeBool("races", false)));
        
        // Used for creating the data
        public CharacterInfo(
            Identifier speciesName,
            string name = "",
            string originalName = "",
            Either<Job, JobPrefab> jobOrJobPrefab = null,
            string ragdollFileName = null,
            int variant = 0,
            Rand.RandSync randSync = Rand.RandSync.Unsynced,
            Identifier npcIdentifier = default)
        {
            JobPrefab jobPrefab = null;
            Job job = null;
            if (jobOrJobPrefab != null)
            {
                jobOrJobPrefab.TryGet(out job);
                jobOrJobPrefab.TryGet(out jobPrefab);
            }
            ID = idCounter;
            idCounter++;
            SpeciesName = speciesName;
            SpriteTags = new List<Identifier>();
            CharacterConfigElement = CharacterPrefab.FindBySpeciesName(SpeciesName)?.ConfigElement;
            if (CharacterConfigElement == null) { return; }
            // TODO: support for variants
            HasSpecifierTags = ElementHasSpecifierTags(CharacterConfigElement);
            if (HasSpecifierTags)
            {
                HairColors = CharacterConfigElement.GetAttributeTupleArray("haircolors", new (Color, float)[] { (Color.WhiteSmoke, 100f) }).ToImmutableArray();
                FacialHairColors = CharacterConfigElement.GetAttributeTupleArray("facialhaircolors", new (Color, float)[] { (Color.WhiteSmoke, 100f) }).ToImmutableArray();
                SkinColors = CharacterConfigElement.GetAttributeTupleArray("skincolors", new (Color, float)[] { (new Color(255, 215, 200, 255), 100f) }).ToImmutableArray();

                var headPreset = Prefab.Heads.GetRandom(randSync);
                Head = new HeadInfo(this, headPreset);
                SetAttachments(randSync);
                SetColors(randSync);
                
                Job = job ?? ((jobPrefab == null) ? Job.Random(Rand.RandSync.Unsynced) : new Job(jobPrefab, randSync, variant));

                if (!string.IsNullOrEmpty(name))
                {
                    Name = name;
                }
                else if (!npcIdentifier.IsEmpty && TextManager.Get("npctitle." + npcIdentifier) is { Loaded: true } npcTitle)
                {
                    Name = npcTitle.Value;
                }
                else
                {
                    Name = GetRandomName(randSync);
                }
                
                SetPersonalityTrait();

                Salary = CalculateSalary();
            }
            OriginalName = !string.IsNullOrEmpty(originalName) ? originalName : Name;
            if (ragdollFileName != null)
            {
                this.ragdollFileName = ragdollFileName;
            }
        }

        private void SetPersonalityTrait()
            => PersonalityTrait = NPCPersonalityTrait.GetRandom(Name + string.Concat(Head.Preset.TagSet));

        public string GetRandomName(Rand.RandSync randSync)
        {
            GetName(randSync, out string name);

            return name;
        }

        public static Color SelectRandomColor(in ImmutableArray<(Color Color, float Commonness)> array, Rand.RandSync randSync)
            => ToolBox.SelectWeightedRandom(array, array.Select(p => p.Commonness).ToArray(), randSync)
                .Color;

        private void SetAttachments(Rand.RandSync randSync)
        {
            LoadHeadAttachments();

            int pickRandomIndex(IReadOnlyList<ContentXElement> list)
            {
                var elems = GetValidAttachmentElements(list, Head.Preset).ToArray();
                var weights = GetWeights(elems).ToArray();
                return list.IndexOf(ToolBox.SelectWeightedRandom(elems, weights, randSync));
            }

            Head.HairIndex = pickRandomIndex(Hairs);
            Head.BeardIndex = pickRandomIndex(Beards);
            Head.MoustacheIndex = pickRandomIndex(Moustaches);
            Head.FaceAttachmentIndex = pickRandomIndex(FaceAttachments);
        }
        
        private void SetColors(Rand.RandSync randSync)
        {
            Head.HairColor = SelectRandomColor(HairColors, randSync);
            Head.FacialHairColor = SelectRandomColor(FacialHairColors, randSync);
            Head.SkinColor = SelectRandomColor(SkinColors, randSync);
        }

        private bool IsColorValid(in Color clr)
            => clr.R != 0 || clr.G != 0 || clr.B != 0;
        
        private void CheckColors()
        {
            if (!IsColorValid(Head.HairColor))
            {
                Head.HairColor = SelectRandomColor(HairColors, Rand.RandSync.Unsynced);
            }
            if (!IsColorValid(Head.FacialHairColor))
            {
                Head.FacialHairColor = SelectRandomColor(FacialHairColors, Rand.RandSync.Unsynced);
            }
            if (!IsColorValid(Head.SkinColor))
            {
                Head.SkinColor = SelectRandomColor(SkinColors, Rand.RandSync.Unsynced);
            }
        }

        // Used for loading the data
        public CharacterInfo(XElement infoElement)
        {
            ID = idCounter;
            idCounter++;
            Name = infoElement.GetAttributeString("name", "");
            OriginalName = infoElement.GetAttributeString("originalname", null);
            Salary = infoElement.GetAttributeInt("salary", 1000);

            ExperiencePoints = infoElement.GetAttributeInt("experiencepoints", 0);
            UnlockedTalents = new HashSet<Identifier>(infoElement.GetAttributeIdentifierArray("unlockedtalents", Array.Empty<Identifier>()));
            AdditionalTalentPoints = infoElement.GetAttributeInt("additionaltalentpoints", 0);
            HashSet<Identifier> tags = infoElement.GetAttributeIdentifierArray("tags", Array.Empty<Identifier>()).ToHashSet();
            LoadTagsBackwardsCompatibility(infoElement, tags);
            SpeciesName = infoElement.GetAttributeIdentifier("speciesname", "");
            ContentXElement element;
            if (!SpeciesName.IsEmpty)
            {
                element = CharacterPrefab.FindBySpeciesName(SpeciesName)?.ConfigElement;
            }
            else
            {
                // Backwards support (human only)
                // Actually you know what this is backwards!
                throw new InvalidOperationException("SpeciesName not defined");
            }
            if (element == null) { return; }
            // TODO: support for variants
            CharacterConfigElement = element;
            HasSpecifierTags = ElementHasSpecifierTags(CharacterConfigElement);
            if (HasSpecifierTags)
            {
                RecreateHead(
                    tags.ToImmutableHashSet(),
                    infoElement.GetAttributeInt("hairindex", -1),
                    infoElement.GetAttributeInt("beardindex", -1),
                    infoElement.GetAttributeInt("moustacheindex", -1),
                    infoElement.GetAttributeInt("faceattachmentindex", -1));

                HairColors = CharacterConfigElement.GetAttributeTupleArray("haircolors", new (Color, float)[] { (Color.WhiteSmoke, 100f) }).ToImmutableArray();
                FacialHairColors = CharacterConfigElement.GetAttributeTupleArray("facialhaircolors", new (Color, float)[] { (Color.WhiteSmoke, 100f) }).ToImmutableArray();
                SkinColors = CharacterConfigElement.GetAttributeTupleArray("skincolors", new (Color, float)[] { (new Color(255, 215, 200, 255), 100f) }).ToImmutableArray();
                
                Head.SkinColor = infoElement.GetAttributeColor("skincolor", Color.White);
                Head.HairColor = infoElement.GetAttributeColor("haircolor", Color.White);
                Head.FacialHairColor = infoElement.GetAttributeColor("facialhaircolor", Color.White);
                CheckColors();

                if (string.IsNullOrEmpty(Name))
                {
                    var nameElement = CharacterConfigElement.GetChildElement("names");
                    if (nameElement != null)
                    {
                        GetName(Rand.RandSync.ServerAndClient, out Name);
                    }
                }
            }

            if (string.IsNullOrEmpty(OriginalName))
            {
                OriginalName = Name;
            }

            StartItemsGiven = infoElement.GetAttributeBool("startitemsgiven", false);
            Identifier personalityName = infoElement.GetAttributeIdentifier("personality", "");
            ragdollFileName = infoElement.GetAttributeString("ragdoll", string.Empty);
            if (personalityName != Identifier.Empty)
            {
                PersonalityTrait = NPCPersonalityTrait.Get(GameSettings.CurrentConfig.Language, personalityName);
            }

            MissionsCompletedSinceDeath = infoElement.GetAttributeInt("missionscompletedsincedeath", 0);

            foreach (var subElement in infoElement.Elements())
            {
                bool jobCreated = false;
                if (subElement.Name.ToString().Equals("job", StringComparison.OrdinalIgnoreCase) && !jobCreated)
                {
                    Job = new Job(subElement);
                    jobCreated = true;
                    // there used to be a break here, but it had to be removed to make room for statvalues
                    // using the jobCreated boolean to make sure that only the first job found is created
                }
                else if (subElement.Name.ToString().Equals("savedstatvalues", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (XElement savedStat in subElement.Elements())
                    {
                        string statTypeString = savedStat.GetAttributeString("stattype", "").ToLowerInvariant();
                        if (!Enum.TryParse(statTypeString, true, out StatTypes statType))
                        {
                            DebugConsole.ThrowError("Invalid stat type type \"" + statTypeString + "\" when loading character data in CharacterInfo!");
                            continue;
                        }

                        float value = savedStat.GetAttributeFloat("statvalue", 0f);
                        if (value == 0f) { continue; }

                        string statIdentifier = savedStat.GetAttributeString("statidentifier", "").ToLowerInvariant();
                        if (string.IsNullOrEmpty(statIdentifier))
                        {
                            DebugConsole.ThrowError("Stat identifier not specified for Stat Value when loading character data in CharacterInfo!");
                            return;
                        }

                        bool removeOnDeath = savedStat.GetAttributeBool("removeondeath", true);
                        ChangeSavedStatValue(statType, value, statIdentifier, removeOnDeath);
                    }
                }
            }
            LoadHeadAttachments();
        }

        private List<ContentXElement> hairs;
        public IReadOnlyList<ContentXElement> Hairs => hairs;
        private List<ContentXElement> beards;
        public IReadOnlyList<ContentXElement> Beards => beards;
        private List<ContentXElement> moustaches;
        public IReadOnlyList<ContentXElement> Moustaches => moustaches;
        private List<ContentXElement> faceAttachments;
        public IReadOnlyList<ContentXElement> FaceAttachments => faceAttachments;

        private IEnumerable<ContentXElement> wearables;
        public IEnumerable<ContentXElement> Wearables
        {
            get
            {
                if (wearables == null)
                {
                    var attachments = CharacterConfigElement.GetChildElement("HeadAttachments");
                    if (attachments != null)
                    {
                        wearables = attachments.GetChildElements("Wearable");
                    }
                }
                return wearables;
            }
        }

        public int GetIdentifier()
        {
            return GetIdentifier(Name);
        }

        public int GetIdentifierUsingOriginalName()
        {
            return GetIdentifier(OriginalName);
        }

        private int GetIdentifier(string name)
        {
            int id = ToolBox.StringToInt(name + string.Join("", Head.Preset.TagSet.OrderBy(s => s)));
            id ^= Head.HairIndex << 12;
            id ^= Head.BeardIndex << 18;
            id ^= Head.MoustacheIndex << 24;
            id ^= Head.FaceAttachmentIndex << 30;
            if (Job != null)
            {
                id ^= ToolBox.StringToInt(Job.Prefab.Identifier.Value);
            }
            return id;
        }

        public IEnumerable<ContentXElement> FilterElements(IEnumerable<ContentXElement> elements, ImmutableHashSet<Identifier> tags, WearableType? targetType = null)
        {
            if (elements is null) { return null; }
            return elements.Where(w =>
            {
                if (!(targetType is null))
                {
                    if (Enum.TryParse(w.GetAttributeString("type", ""), true, out WearableType type) && type != targetType) { return false; }
                }
                HashSet<Identifier> t = w.GetAttributeIdentifierArray("tags", Array.Empty<Identifier>()).ToHashSet();
                LoadTagsBackwardsCompatibility(w, t);
                return t.IsSubsetOf(tags);
            });
        }

        public void RecreateHead(ImmutableHashSet<Identifier> tags, int hairIndex, int beardIndex, int moustacheIndex, int faceAttachmentIndex)
        {
            HeadPreset headPreset = Prefab.Heads.FirstOrDefault(h => h.TagSet.SetEquals(tags));
            if (headPreset == null) 
            {
                if (tags.Count == 1)
                {
                    headPreset = Prefab.Heads.FirstOrDefault(h => h.TagSet.Contains(tags.First()));
                }
                headPreset ??= Prefab.Heads.GetRandomUnsynced(); 
            }
            head = new HeadInfo(this, headPreset, hairIndex, beardIndex, moustacheIndex, faceAttachmentIndex);
            ReloadHeadAttachments();
        }

        public string ReplaceVars(string str)
        {
            return Prefab.ReplaceVars(str, Head.Preset);
        }

#if CLIENT
        public void RecreateHead(MultiplayerPreferences characterSettings)
        {
            if (characterSettings.HairIndex == -1 && 
                characterSettings.BeardIndex == -1 && 
                characterSettings.MoustacheIndex == -1 && 
                characterSettings.FaceAttachmentIndex == -1)
            {
                //randomize if nothing is set
                SetAttachments(Rand.RandSync.Unsynced);
                characterSettings.HairIndex = Head.HairIndex;
                characterSettings.BeardIndex = Head.BeardIndex;
                characterSettings.MoustacheIndex = Head.MoustacheIndex;
                characterSettings.FaceAttachmentIndex = Head.FaceAttachmentIndex;
            }

            RecreateHead(
                characterSettings.TagSet.ToImmutableHashSet(),
                characterSettings.HairIndex,
                characterSettings.BeardIndex,
                characterSettings.MoustacheIndex,
                characterSettings.FaceAttachmentIndex);

            Head.SkinColor = ChooseColor(SkinColors, characterSettings.SkinColor);
            Head.HairColor = ChooseColor(HairColors, characterSettings.HairColor);
            Head.FacialHairColor = ChooseColor(FacialHairColors, characterSettings.FacialHairColor);

            Color ChooseColor(in ImmutableArray<(Color Color, float Commonness)> availableColors, Color chosenColor)
            {
                return availableColors.Any(c => c.Color == chosenColor) ? chosenColor : SelectRandomColor(availableColors, Rand.RandSync.Unsynced);
            }
        }
#endif
        
        public void RecreateHead(HeadInfo headInfo)
        {
            RecreateHead(
                headInfo.Preset.TagSet,
                headInfo.HairIndex,
                headInfo.BeardIndex,
                headInfo.MoustacheIndex,
                headInfo.FaceAttachmentIndex);

            Head.SkinColor = headInfo.SkinColor;
            Head.HairColor = headInfo.HairColor;
            Head.FacialHairColor = headInfo.FacialHairColor;
            CheckColors();
        }

        /// <summary>
        /// Reloads the head sprite and the attachment sprites.
        /// </summary>
        public void RefreshHead()
        {
            ReloadHeadAttachments();
            RefreshHeadSprites();
        }

        partial void LoadHeadSpriteProjectSpecific(ContentXElement limbElement);
        
        private void LoadHeadSprite()
        {
            foreach (var limbElement in Ragdoll.MainElement.Elements())
            {
                if (!limbElement.GetAttributeString("type", string.Empty).Equals("head", StringComparison.OrdinalIgnoreCase)) { continue; }

                ContentXElement spriteElement = limbElement.GetChildElement("sprite");
                if (spriteElement == null) { continue; }

                string spritePath = spriteElement.GetAttributeContentPath("texture")?.Value;
                if (string.IsNullOrEmpty(spritePath)) { continue; }

                spritePath = ReplaceVars(spritePath);

                string fileName = Path.GetFileNameWithoutExtension(spritePath);

                if (string.IsNullOrEmpty(fileName)) { continue; }

                //go through the files in the directory to find a matching sprite
                foreach (string file in Directory.GetFiles(Path.GetDirectoryName(spritePath)))
                {
                    if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string fileWithoutTags = Path.GetFileNameWithoutExtension(file);
                    fileWithoutTags = fileWithoutTags.Split('[', ']').First();
                    if (fileWithoutTags != fileName) { continue; }

                    HeadSprite = new Sprite(spriteElement, "", file);
                    Portrait = new Sprite(spriteElement, "", file) { RelativeOrigin = Vector2.Zero };

                    //extract the tags out of the filename
                    SpriteTags = file.Split('[', ']').Skip(1).Select(id => id.ToIdentifier()).ToList();
                    if (SpriteTags.Any())
                    {
                        SpriteTags.RemoveAt(SpriteTags.Count - 1);
                    }

                    break;
                }

                LoadHeadSpriteProjectSpecific(limbElement);

                break;
            }
        }

        public void LoadHeadAttachments()
        {
            if (Wearables != null)
            {
                if (hairs == null)
                {
                    float commonness = 0.1f;
                    hairs = AddEmpty(FilterElements(wearables, head.Preset.TagSet, WearableType.Hair), WearableType.Hair, commonness);
                }
                if (beards == null)
                {
                    beards = AddEmpty(FilterElements(wearables, head.Preset.TagSet, WearableType.Beard), WearableType.Beard);
                }
                if (moustaches == null)
                {
                    moustaches = AddEmpty(FilterElements(wearables, head.Preset.TagSet, WearableType.Moustache), WearableType.Moustache);
                }
                if (faceAttachments == null)
                {
                    faceAttachments = AddEmpty(FilterElements(wearables, head.Preset.TagSet, WearableType.FaceAttachment), WearableType.FaceAttachment);
                }
            }
        }

        public static List<ContentXElement> AddEmpty(IEnumerable<ContentXElement> elements, WearableType type, float commonness = 1)
        {
            // Let's add an empty element so that there's a chance that we don't get any actual element -> allows bald and beardless guys, for example.
            var emptyElement = new XElement("EmptyWearable", type.ToString(), new XAttribute("commonness", commonness)).FromPackage(null);
            var list = new List<ContentXElement>() { emptyElement };
            list.AddRange(elements);
            return list;
        }

        public ContentXElement GetRandomElement(IEnumerable<ContentXElement> elements)
        {
            var filtered = elements.Where(IsWearableAllowed);
            if (filtered.Count() == 0) { return null; }
            var element = ToolBox.SelectWeightedRandom(filtered.ToList(), GetWeights(filtered).ToList(), Rand.RandSync.Unsynced);
            return element == null || element.NameAsIdentifier() == "Empty" ? null : element;
        }

        private bool IsWearableAllowed(ContentXElement element)
        {
            string spriteName = element.GetChildElement("sprite").GetAttributeString("name", string.Empty);
            return IsAllowed(Head.HairElement, spriteName) && IsAllowed(Head.BeardElement, spriteName) && IsAllowed(Head.MoustacheElement, spriteName) && IsAllowed(Head.FaceAttachment, spriteName);
        }

        private bool IsAllowed(XElement element, string spriteName)
        {
            if (element != null)
            {
                var disallowed = element.GetAttributeStringArray("disallow", Array.Empty<string>());
                if (disallowed.Any(s => spriteName.Contains(s)))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool IsValidIndex(int index, List<ContentXElement> list) => index >= 0 && index < list.Count;

        private static IEnumerable<float> GetWeights(IEnumerable<ContentXElement> elements) => elements.Select(h => h.GetAttributeFloat("commonness", 1f));

        partial void LoadAttachmentSprites();
        
        private int CalculateSalary()
        {
            if (Name == null || Job == null) { return 0; }

            int salary = 0;
            foreach (Skill skill in Job.GetSkills())
            {
                salary += (int)(skill.Level * skill.PriceMultiplier);
            }

            return (int)(salary * Job.Prefab.PriceMultiplier);
        }

        public void IncreaseSkillLevel(Identifier skillIdentifier, float increase, bool gainedFromAbility = false)
        {
            if (Job == null || (GameMain.NetworkMember != null && GameMain.NetworkMember.IsClient) || Character == null) { return; }

            if (Job.Prefab.Identifier == "assistant")
            {
                increase *= SkillSettings.Current.AssistantSkillIncreaseMultiplier;
            }

            increase *= 1f + Character.GetStatValue(StatTypes.SkillGainSpeed);

            float prevLevel = Job.GetSkillLevel(skillIdentifier);
            Job.IncreaseSkillLevel(skillIdentifier, increase, Character.HasAbilityFlag(AbilityFlags.GainSkillPastMaximum));

            float newLevel = Job.GetSkillLevel(skillIdentifier);

            if ((int)newLevel > (int)prevLevel)
            {                
                // assume we are getting at least 1 point in skill, since this logic only runs in such cases
                float increaseSinceLastSkillPoint = MathHelper.Max(increase, 1f);
                var abilitySkillGain = new AbilitySkillGain(increaseSinceLastSkillPoint, skillIdentifier, Character, gainedFromAbility);
                Character.CheckTalents(AbilityEffectType.OnGainSkillPoint, abilitySkillGain);
                foreach (Character character in Character.GetFriendlyCrew(Character))
                {
                    character.CheckTalents(AbilityEffectType.OnAllyGainSkillPoint, abilitySkillGain);
                }
            }

            OnSkillChanged(skillIdentifier, prevLevel, newLevel);
        }

        public void SetSkillLevel(Identifier skillIdentifier, float level)
        {
            if (Job == null) { return; }

            var skill = Job.GetSkill(skillIdentifier);
            if (skill == null)
            {
                Job.IncreaseSkillLevel(skillIdentifier, level, increasePastMax: false);
                OnSkillChanged(skillIdentifier, 0.0f, level);
            }
            else
            {
                float prevLevel = skill.Level;
                skill.Level = level;
                OnSkillChanged(skillIdentifier, prevLevel, skill.Level);
            }
        }

        partial void OnSkillChanged(Identifier skillIdentifier, float prevLevel, float newLevel);

        public void GiveExperience(int amount, bool isMissionExperience = false)
        {
            int prevAmount = ExperiencePoints;

            var experienceGainMultiplier = new AbilityExperienceGainMultiplier(1f);
            if (isMissionExperience)
            {
                Character?.CheckTalents(AbilityEffectType.OnGainMissionExperience, experienceGainMultiplier);
            }
            experienceGainMultiplier.Value += Character?.GetStatValue(StatTypes.ExperienceGainMultiplier) ?? 0;

            amount = (int)(amount * experienceGainMultiplier.Value);
            if (amount < 0) { return; }

            ExperiencePoints += amount;
            OnExperienceChanged(prevAmount, ExperiencePoints);
        }

        public void SetExperience(int newExperience)
        {
            if (newExperience < 0) { return; }

            int prevAmount = ExperiencePoints;
            ExperiencePoints = newExperience;
            OnExperienceChanged(prevAmount, ExperiencePoints);
        }

        const int BaseExperienceRequired = -50;
        const int AddedExperienceRequiredPerLevel = 500;

        public int GetTotalTalentPoints()
        {
            return GetCurrentLevel() + AdditionalTalentPoints - 1;
        }

        public int GetAvailableTalentPoints()
        {
            // hashset always has at least 1 
            return Math.Max(GetTotalTalentPoints() - GetUnlockedTalentsInTree().Count(), 0);
        }

        public float GetProgressTowardsNextLevel()
        {
            return (ExperiencePoints - GetExperienceRequiredForCurrentLevel()) / (float)(GetExperienceRequiredToLevelUp() - GetExperienceRequiredForCurrentLevel());
        }

        public int GetExperienceRequiredForCurrentLevel()
        {
            GetCurrentLevel(out int experienceRequired);
            return experienceRequired;
        }

        public int GetExperienceRequiredToLevelUp()
        {
            int level = GetCurrentLevel(out int experienceRequired);
            return experienceRequired + ExperienceRequiredPerLevel(level);
        }

        public int GetCurrentLevel()
        {
            return GetCurrentLevel(out _);
        }

        private int GetCurrentLevel(out int experienceRequired)
        {
            int level = 1;
            experienceRequired = 0;
            while (experienceRequired + ExperienceRequiredPerLevel(level) <= ExperiencePoints)
            {
                experienceRequired += ExperienceRequiredPerLevel(level);
                level++;
            }
            return level;
        }

        private int ExperienceRequiredPerLevel(int level)
        {
            return BaseExperienceRequired + AddedExperienceRequiredPerLevel * level;
        }

        partial void OnExperienceChanged(int prevAmount, int newAmount);

        partial void OnPermanentStatChanged(StatTypes statType);

        public void Rename(string newName)
        {
            if (string.IsNullOrEmpty(newName)) { return; }
            // Replace the name tag of any existing id cards or duffel bags
            foreach (var item in Item.ItemList)
            {
                if (!item.HasTag("identitycard") && !item.HasTag("despawncontainer")) { continue; }
                foreach (var tag in item.Tags.Split(','))
                {
                    var splitTag = tag.Split(":");
                    if (splitTag.Length < 2) { continue; }
                    if (splitTag[0] != "name") { continue; }
                    if (splitTag[1] != Name) { continue; }
                    item.ReplaceTag(tag, $"name:{newName}");
                    break;
                }
            }
            Name = newName;
        }

        public void ResetName()
        {
            Name = OriginalName;
        }

        public XElement Save(XElement parentElement)
        {
            XElement charElement = new XElement("Character");

            charElement.Add(
                new XAttribute("name", Name),
                new XAttribute("originalname", OriginalName),
                new XAttribute("speciesname", SpeciesName),
                new XAttribute("tags", string.Join(",", Head.Preset.TagSet)),
                new XAttribute("salary", Salary),
                new XAttribute("experiencepoints", ExperiencePoints),
                new XAttribute("unlockedtalents", string.Join(",", UnlockedTalents)),
                new XAttribute("additionaltalentpoints", AdditionalTalentPoints),
                new XAttribute("hairindex", Head.HairIndex),
                new XAttribute("beardindex", Head.BeardIndex),
                new XAttribute("moustacheindex", Head.MoustacheIndex),
                new XAttribute("faceattachmentindex", Head.FaceAttachmentIndex),
                new XAttribute("skincolor", XMLExtensions.ColorToString(Head.SkinColor)),
                new XAttribute("haircolor", XMLExtensions.ColorToString(Head.HairColor)),
                new XAttribute("facialhaircolor", XMLExtensions.ColorToString(Head.FacialHairColor)),
                new XAttribute("startitemsgiven", StartItemsGiven),
                new XAttribute("ragdoll", ragdollFileName),
                new XAttribute("personality", PersonalityTrait?.Name.Value ?? ""));
                // TODO: animations?

            charElement.Add(new XAttribute("missionscompletedsincedeath", MissionsCompletedSinceDeath));

            if (Character != null)
            {
                if (Character.AnimController.CurrentHull != null)
                {
                    charElement.Add(new XAttribute("hull", Character.AnimController.CurrentHull.ID));
                }
            }
            
            Job.Save(charElement);

            XElement savedStatElement = new XElement("savedstatvalues");
            foreach (var statValuePair in SavedStatValues)
            {
                foreach (var savedStat in statValuePair.Value)
                {
                    if (savedStat.StatValue == 0f) { continue; }

                    savedStatElement.Add(new XElement("savedstatvalue",
                        new XAttribute("stattype", statValuePair.Key.ToString()),
                        new XAttribute("statidentifier", savedStat.StatIdentifier),
                        new XAttribute("statvalue", savedStat.StatValue),
                        new XAttribute("removeondeath", savedStat.RemoveOnDeath)
                        ));
                }
            }



            charElement.Add(savedStatElement);

            parentElement.Add(charElement);
            return charElement;
        }

        public static void SaveOrders(XElement parentElement, params Order[] orders)
        {
            if (parentElement == null || orders == null || orders.None()) { return; }
            // If an order is invalid, we discard the order and increase the priority of the following orders so
            // 1) the highest priority value will remain equal to CharacterInfo.HighestManualOrderPriority; and
            // 2) the order priorities will remain sequential.
            int priorityIncrease = 0;
            var linkedSubs = GetLinkedSubmarines();
            foreach (var orderInfo in orders)
            {
                var order = orderInfo;
                if (order == null || order.Identifier == Identifier.Empty)
                {
                    DebugConsole.ThrowError("Error saving an order - the order or its identifier is null");
                    priorityIncrease++;
                    continue;
                }
                int? linkedSubIndex = null;
                bool targetAvailableInNextLevel = true;
                if (order.TargetSpatialEntity != null)
                {
                    var entitySub = order.TargetSpatialEntity.Submarine;
                    bool isOutside = entitySub == null;
                    bool canBeOnLinkedSub = !isOutside && Submarine.MainSub != null && entitySub != Submarine.MainSub && linkedSubs.Any();
                    bool isOnConnectedLinkedSub = false;
                    if (canBeOnLinkedSub)
                    {
                        for (int i = 0; i < linkedSubs.Count; i++)
                        {
                            var ls = linkedSubs[i];
                            if (!ls.LoadSub) { continue; }
                            if (ls.Sub != entitySub) { continue; }
                            linkedSubIndex = i;
                            isOnConnectedLinkedSub = Submarine.MainSub.GetConnectedSubs().Contains(entitySub);
                            break;
                        }
                    }
                    targetAvailableInNextLevel = !isOutside && GameMain.GameSession?.Campaign?.PendingSubmarineSwitch == null && (isOnConnectedLinkedSub || entitySub == Submarine.MainSub);
                    if (!targetAvailableInNextLevel)
                    {
                        if (!order.Prefab.CanBeGeneralized)
                        {
                            DebugConsole.Log($"Trying to save an order ({order.Identifier}) targeting an entity that won't be connected to the main sub in the next level. The order requires a target so it won't be saved.");
                            priorityIncrease++;
                            continue;
                        }
                        else
                        {
                            DebugConsole.Log($"Saving an order ({order.Identifier}) targeting an entity that won't be connected to the main sub in the next level. The order will be saved as a generalized version.");
                        }
                    }
                }
                if (orderInfo.ManualPriority < 1)
                {
                    DebugConsole.ThrowError($"Error saving an order ({order.Identifier}) - the order priority is less than 1");
                    priorityIncrease++;
                    continue;
                }
                var orderElement = new XElement("order",
                    new XAttribute("id", order.Identifier),
                    new XAttribute("priority", orderInfo.ManualPriority + priorityIncrease),
                    new XAttribute("targettype", (int)order.TargetType));
                if (orderInfo.Option != Identifier.Empty)
                {
                    orderElement.Add(new XAttribute("option", orderInfo.Option));
                }
                if (order.OrderGiver != null)
                {
                    orderElement.Add(new XAttribute("ordergiverinfoid", order.OrderGiver.Info.ID));
                }
                if (order.TargetSpatialEntity?.Submarine is Submarine targetSub)
                {
                    if (targetSub == Submarine.MainSub)
                    {
                        orderElement.Add(new XAttribute("onmainsub", true));
                    }
                    else if(linkedSubIndex.HasValue)
                    {
                        orderElement.Add(new XAttribute("linkedsubindex", linkedSubIndex));
                    }
                }
                switch (order.TargetType)
                {
                    case Order.OrderTargetType.Entity when targetAvailableInNextLevel && order.TargetEntity is Entity e:
                        orderElement.Add(new XAttribute("targetid", (uint)e.ID));
                        break;
                    case Order.OrderTargetType.Position when targetAvailableInNextLevel && order.TargetSpatialEntity is OrderTarget ot:
                        var orderTargetElement = new XElement("ordertarget");
                        var position = ot.WorldPosition;
                        if (ot.Hull != null)
                        {
                            orderTargetElement.Add(new XAttribute("hullid", (uint)ot.Hull.ID));
                            position -= ot.Hull.WorldPosition;
                        }
                        orderTargetElement.Add(new XAttribute("position", XMLExtensions.Vector2ToString(position)));
                        orderElement.Add(orderTargetElement);
                        break;
                    case Order.OrderTargetType.WallSection when targetAvailableInNextLevel && order.TargetEntity is Structure s && order.WallSectionIndex.HasValue:
                        orderElement.Add(new XAttribute("structureid", s.ID));
                        orderElement.Add(new XAttribute("wallsectionindex", order.WallSectionIndex.Value));
                        break;
                }
                parentElement.Add(orderElement);
            }
        }

        /// <summary>
        /// Save current orders to the parameter element
        /// </summary>
        public static void SaveOrderData(CharacterInfo characterInfo, XElement parentElement)
        {
            var currentOrders = new List<Order>(characterInfo.CurrentOrders);
            // Sort the current orders to make sure the one with the highest priority comes first
            currentOrders.Sort((x, y) => y.ManualPriority.CompareTo(x.ManualPriority));
            SaveOrders(parentElement, currentOrders.ToArray());
        }

        /// <summary>
        /// Save current orders to <see cref="OrderData"/>
        /// </summary>
        public void SaveOrderData()
        {
            OrderData = new XElement("orders");
            SaveOrderData(this, OrderData);
        }

        public static void ApplyOrderData(Character character, XElement orderData)
        {
            if (character == null) { return; }
            var orders = LoadOrders(orderData);
            foreach (var order in orders)
            {
                character.SetOrder(order, isNewOrder: true, speak: false, force: true);
            }
        }

        public void ApplyOrderData()
        {
            ApplyOrderData(Character, OrderData);
        }

        public static List<Order> LoadOrders(XElement ordersElement)
        {
            var orders = new List<Order>();
            if (ordersElement == null) { return orders; }
            // If an order is invalid, we discard the order and increase the priority of the following orders so
            // 1) the highest priority value will remain equal to CharacterInfo.HighestManualOrderPriority; and
            // 2) the order priorities will remain sequential.
            int priorityIncrease = 0;
            var linkedSubs = GetLinkedSubmarines();
            foreach (var orderElement in ordersElement.GetChildElements("order"))
            {
                Order order = null;
                string orderIdentifier = orderElement.GetAttributeString("id", "");
                var orderPrefab = OrderPrefab.Prefabs[orderIdentifier];
                if (orderPrefab == null)
                {
                    DebugConsole.ThrowError($"Error loading a previously saved order - can't find an order prefab with the identifier \"{orderIdentifier}\"");
                    priorityIncrease++;
                    continue;
                }
                var targetType = (Order.OrderTargetType)orderElement.GetAttributeInt("targettype", 0);
                int orderGiverInfoId = orderElement.GetAttributeInt("ordergiverinfoid", -1);
                var orderGiver = orderGiverInfoId >= 0 ? Character.CharacterList.FirstOrDefault(c => c.Info?.ID == orderGiverInfoId) : null;
                Entity targetEntity = null;
                switch (targetType)
                {
                    case Order.OrderTargetType.Entity:
                        ushort targetId = (ushort)orderElement.GetAttributeUInt("targetid", Entity.NullEntityID);
                        if (!GetTargetEntity(targetId, out targetEntity)) { continue; }
                        var targetComponent = orderPrefab.GetTargetItemComponent(targetEntity as Item);
                        order = new Order(orderPrefab, targetEntity, targetComponent, orderGiver: orderGiver);
                        break;
                    case Order.OrderTargetType.Position:
                        var orderTargetElement = orderElement.GetChildElement("ordertarget");
                        var position = orderTargetElement.GetAttributeVector2("position", Vector2.Zero);
                        ushort hullId = (ushort)orderTargetElement.GetAttributeUInt("hullid", 0);
                        if (!GetTargetEntity(hullId, out targetEntity)) { continue; }
                        if (!(targetEntity is Hull targetPositionHull))
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}) - entity with the ID {hullId} is of type {targetEntity?.GetType()} instead of Hull");
                            priorityIncrease++;
                            continue;
                        }
                        var orderTarget = new OrderTarget(targetPositionHull.WorldPosition + position, targetPositionHull);
                        order = new Order(orderPrefab, orderTarget, orderGiver: orderGiver);
                        break;
                    case Order.OrderTargetType.WallSection:
                        ushort structureId = (ushort)orderElement.GetAttributeInt("structureid", Entity.NullEntityID);
                        if (!GetTargetEntity(structureId, out targetEntity)) { continue; }
                        int wallSectionIndex = orderElement.GetAttributeInt("wallsectionindex", 0);
                        if (!(targetEntity is Structure targetStructure))
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}) - entity with the ID {structureId} is of type {targetEntity?.GetType()} instead of Structure");
                            priorityIncrease++;
                            continue;
                        }
                        order = new Order(orderPrefab, targetStructure, wallSectionIndex, orderGiver: orderGiver);
                        break;
                }
                Identifier orderOption = orderElement.GetAttributeIdentifier("option", "");
                int manualPriority = orderElement.GetAttributeInt("priority", 0) + priorityIncrease;
                var orderInfo = order.WithOption(orderOption).WithManualPriority(manualPriority);
                orders.Add(orderInfo);

                bool GetTargetEntity(ushort targetId, out Entity targetEntity)
                {
                    targetEntity = null;
                    if (targetId == Entity.NullEntityID) { return true; }
                    Submarine parentSub = null;
                    if (orderElement.GetAttributeBool("onmainsub", false))
                    {
                        parentSub = Submarine.MainSub;
                    }
                    else
                    {
                        int linkedSubIndex = orderElement.GetAttributeInt("linkedsubindex", -1);
                        if (linkedSubIndex >= 0 && linkedSubIndex < linkedSubs.Count &&
                            linkedSubs[linkedSubIndex] is LinkedSubmarine linkedSub && linkedSub.LoadSub)
                        {
                            parentSub = linkedSub.Sub;
                        }
                    }
                    if (parentSub != null)
                    {
                        targetId = GetOffsetId(parentSub, targetId);
                        targetEntity = Entity.FindEntityByID(targetId);
                    }
                    else
                    {
                        if (!orderPrefab.CanBeGeneralized)
                        {
                            DebugConsole.ThrowError($"Error loading a previously saved order ({orderIdentifier}). Can't find the parent sub of the target entity. The order requires a target so it can't be loaded at all.");
                            priorityIncrease++;
                            return false;
                        }
                        else
                        {
                            DebugConsole.AddWarning($"Trying to load a previously saved order ({orderIdentifier}). Can't find the parent sub of the target entity. The order doesn't require a target so a more generic version of the order will be loaded instead.");
                        }
                    }
                    return true;
                }
            }
            return orders;
        }

        private static List<LinkedSubmarine> GetLinkedSubmarines()
        {
            return Entity.GetEntities()
                .OfType<LinkedSubmarine>()
                .Where(ls => ls.Submarine == Submarine.MainSub)
                .OrderBy(e => e.ID)
                .ToList();
        }

        private static ushort GetOffsetId(Submarine parentSub, ushort id)
        {
            if (parentSub != null)
            {
                var idRemap = new IdRemap(parentSub.Info.SubmarineElement, parentSub.IdOffset);
                return idRemap.GetOffsetId(id);
            }
            return id;
        }

        public static void ApplyHealthData(Character character, XElement healthData)
        {
            if (healthData != null) { character?.CharacterHealth.Load(healthData); }
        }

        /// <summary>
        /// Reloads the attachment xml elements according to the indices. Doesn't reload the sprites.
        /// </summary>
        public void ReloadHeadAttachments()
        {
            ResetLoadedAttachments();
            LoadHeadAttachments();
        }

        private void ResetAttachmentIndices()
        {
            Head.ResetAttachmentIndices();
        }

        private void ResetLoadedAttachments()
        {
            hairs = null;
            beards = null;
            moustaches = null;
            faceAttachments = null;
        }

        public void ClearCurrentOrders()
        {
            CurrentOrders.Clear();
        }

        public void Remove()
        {
            Character = null;
            HeadSprite = null;
            Portrait = null;
            AttachmentSprites = null;
        }

        private void RefreshHeadSprites()
        {
            HeadSprite = null;
            AttachmentSprites = null;
        }

        // This could maybe be a LookUp instead?
        public readonly Dictionary<StatTypes, List<SavedStatValue>> SavedStatValues = new Dictionary<StatTypes, List<SavedStatValue>>();

        public void ClearSavedStatValues()
        {
            foreach (StatTypes statType in SavedStatValues.Keys)
            {
                OnPermanentStatChanged(statType);
            }
            SavedStatValues.Clear();
        }

        public void ClearSavedStatValues(StatTypes statType)
        {
            SavedStatValues.Remove(statType);
            OnPermanentStatChanged(statType);
        }

        public void RemoveSavedStatValuesOnDeath()
        {
            foreach (StatTypes statType in SavedStatValues.Keys)
            {
                foreach (SavedStatValue savedStatValue in SavedStatValues[statType])
                {
                    if (!savedStatValue.RemoveOnDeath) { continue; }
                    if (MathUtils.NearlyEqual(savedStatValue.StatValue, 0.0f)) { continue; }
                    savedStatValue.StatValue = 0.0f;
                    // no need to make a network update, as this is only done after the character has died
                }
            }
        }

        public void ResetSavedStatValue(string statIdentifier)
        {
            foreach (StatTypes statType in SavedStatValues.Keys)
            {
                bool changed = false;
                foreach (SavedStatValue savedStatValue in SavedStatValues[statType])
                {
                    if (savedStatValue.StatIdentifier != statIdentifier) { continue; }
                    if (MathUtils.NearlyEqual(savedStatValue.StatValue, 0.0f)) { continue; }
                    savedStatValue.StatValue = 0.0f;
                    changed = true;
                }
                if (changed) { OnPermanentStatChanged(statType); }
            }
        }

        public float GetSavedStatValue(StatTypes statType)
        {
            if (SavedStatValues.TryGetValue(statType, out var statValues))
            {
                return statValues.Sum(v => v.StatValue);
            }
            else
            {
                return 0f;
            }
        }
        public float GetSavedStatValue(StatTypes statType, Identifier statIdentifier)
        {
            if (SavedStatValues.TryGetValue(statType, out var statValues))
            {
                return statValues.Where(s => s.StatIdentifier == statIdentifier).Sum(v => v.StatValue);
            }
            else
            {
                return 0f;
            }
        }

        public void ChangeSavedStatValue(StatTypes statType, float value, string statIdentifier, bool removeOnDeath, float maxValue = float.MaxValue, bool setValue = false)
        {
            if (!SavedStatValues.ContainsKey(statType))
            {
                SavedStatValues.Add(statType, new List<SavedStatValue>());
            }

            bool changed = false;
            if (SavedStatValues[statType].FirstOrDefault(s => s.StatIdentifier == statIdentifier) is SavedStatValue savedStat)
            {
                float prevValue = savedStat.StatValue;
                savedStat.StatValue = setValue ? value : MathHelper.Min(savedStat.StatValue + value, maxValue);
                changed = !MathUtils.NearlyEqual(savedStat.StatValue, prevValue);
            }
            else
            {
                SavedStatValues[statType].Add(new SavedStatValue(statIdentifier, MathHelper.Min(value, maxValue), removeOnDeath));
                changed = true;
            }
            if (changed) { OnPermanentStatChanged(statType); }
        }
    }

    public class SavedStatValue
    {
        public string StatIdentifier { get; set; }
        public float StatValue { get; set; }
        public bool RemoveOnDeath { get; set; }

        public SavedStatValue(string statIdentifier, float value, bool removeOnDeath)
        {
            StatValue = value;
            RemoveOnDeath = removeOnDeath;
            StatIdentifier = statIdentifier;
        }
    }

    class AbilitySkillGain : AbilityObject, IAbilityValue, IAbilitySkillIdentifier, IAbilityCharacter
    {
        public AbilitySkillGain(float skillAmount, Identifier skillIdentifier, Character character, bool gainedFromAbility)
        {
            Value = skillAmount;
            SkillIdentifier = skillIdentifier;
            Character = character;
            GainedFromAbility = gainedFromAbility;
        }
        public Character Character { get; set; }
        public float Value { get; set; }
        public Identifier SkillIdentifier { get; set; }
        public bool GainedFromAbility { get; }
    }

    class AbilityExperienceGainMultiplier : AbilityObject, IAbilityValue
    {
        public AbilityExperienceGainMultiplier(float experienceGainMultiplier)
        {
            Value = experienceGainMultiplier;
        }
        public float Value { get; set; }
    }
}
