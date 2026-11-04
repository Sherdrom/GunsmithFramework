namespace GunsmithFramework
{
    [HarmonyPatch]
    public static class GunsmithQuickSlotCapacityPatch
    {
        private static readonly Dictionary<string, int> MaxQuickSlotByItemIdentifier = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> QuickSlotTagsByItemIdentifier = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<int>> InjectedSlotsByItemIdentifier = new(StringComparer.OrdinalIgnoreCase);

        internal static void Reset()
        {
            MaxQuickSlotByItemIdentifier.Clear();
            QuickSlotTagsByItemIdentifier.Clear();
            InjectedSlotsByItemIdentifier.Clear();
        }

        public static void RegisterQuickSlotCapacity(string itemIdentifier, int maxSlot, string quickSlotTags)
        {
            if (string.IsNullOrWhiteSpace(itemIdentifier) || maxSlot < 0)
            {
                return;
            }

            if (!MaxQuickSlotByItemIdentifier.TryGetValue(itemIdentifier, out int currentMaxSlot) || maxSlot > currentMaxSlot)
            {
                MaxQuickSlotByItemIdentifier[itemIdentifier] = maxSlot;
            }

            if (!string.IsNullOrWhiteSpace(quickSlotTags))
            {
                QuickSlotTagsByItemIdentifier[itemIdentifier] = quickSlotTags;
            }
        }

        public static bool IsInjectedQuickSlot(string itemIdentifier, int slotIndex)
        {
            return !string.IsNullOrWhiteSpace(itemIdentifier) &&
                   InjectedSlotsByItemIdentifier.TryGetValue(itemIdentifier, out HashSet<int>? slots) &&
                   slots.Contains(slotIndex);
        }

        public static bool HasInjectedQuickSlots(string itemIdentifier)
        {
            return !string.IsNullOrWhiteSpace(itemIdentifier) &&
                   InjectedSlotsByItemIdentifier.TryGetValue(itemIdentifier, out HashSet<int>? slots) &&
                   slots.Count > 0;
        }

        [HarmonyPatch(typeof(Barotrauma.Items.Components.ItemContainer), MethodType.Constructor, typeof(Item), typeof(ContentXElement))]
        [HarmonyPrefix]
        private static void InjectGunsmithQuickSubContainers(Item item, ContentXElement element)
        {
            ContentXElement? gunsmithData = element.Parent?.GetChildElement("GunsmithData");
            string itemIdentifier = item?.Prefab?.Identifier.Value ?? string.Empty;
            bool hasRegisteredMaxSlot = MaxQuickSlotByItemIdentifier.TryGetValue(itemIdentifier, out int registeredMaxSlot);
            int quickSlotStart = gunsmithData?.GetAttributeInt("quickslotstart", hasRegisteredMaxSlot ? 1 : -1) ?? (hasRegisteredMaxSlot ? 1 : -1);
            int maxQuickSlot = hasRegisteredMaxSlot
                ? registeredMaxSlot
                : gunsmithData?.GetAttributeInt("quickslotmax", -1) ?? -1;
            if (quickSlotStart < 0 || maxQuickSlot < quickSlotStart)
            {
                return;
            }

            int declaredCapacity = element.GetAttributeInt("capacity", 0);
            int subContainerCapacity = element.GetChildElements("SubContainer").Sum(sub => sub.GetAttributeInt("capacity", 1));
            int existingCapacity = declaredCapacity + subContainerCapacity;
            int requiredCapacity = maxQuickSlot + 1;
            int missingSlots = requiredCapacity - existingCapacity;

            string items = gunsmithData?.GetAttributeString("quickslotitems", string.Empty) ?? string.Empty;
            QuickSlotTagsByItemIdentifier.TryGetValue(itemIdentifier, out string? registeredTags);
            string tags = gunsmithData?.GetAttributeString("quickslottags", registeredTags ?? string.Empty) ?? registeredTags ?? string.Empty;
            System.Xml.Linq.XElement quickContainable = new("Containable",
                new System.Xml.Linq.XAttribute("hide", "true"),
                new System.Xml.Linq.XAttribute("itempos", "0,0"),
                new System.Xml.Linq.XAttribute("setactive", "true"));
            if (!string.IsNullOrWhiteSpace(items)) { quickContainable.SetAttributeValue("items", items); }
            if (!string.IsNullOrWhiteSpace(tags)) { quickContainable.SetAttributeValue("tags", tags); }

            // Reused XML slots need the same activation and package matching as added slots.
            int slotStart = declaredCapacity;
            foreach (ContentXElement subContainer in element.GetChildElements("SubContainer"))
            {
                int slotEnd = slotStart + subContainer.GetAttributeInt("capacity", 1);
                if (slotStart >= quickSlotStart && slotEnd <= requiredCapacity)
                {
                    foreach (ContentXElement containable in subContainer.GetChildElements("Containable"))
                    {
                        containable.SetAttributeValue("setactive", "true");
                    }
                    if ((!string.IsNullOrWhiteSpace(items) || !string.IsNullOrWhiteSpace(tags)) &&
                        !subContainer.GetChildElements("Containable").Any(c =>
                            c.GetAttributeString("items", string.Empty) == items &&
                            c.GetAttributeString("tags", string.Empty) == tags))
                    {
                        subContainer.Add(new ContentXElement(element.ContentPackage, new System.Xml.Linq.XElement(quickContainable)));
                    }
                }
                slotStart = slotEnd;
            }

            if (missingSlots <= 0)
            {
                return;
            }

            if (!InjectedSlotsByItemIdentifier.TryGetValue(itemIdentifier, out HashSet<int>? injectedSlots))
            {
                injectedSlots = new HashSet<int>();
                InjectedSlotsByItemIdentifier[itemIdentifier] = injectedSlots;
            }

            for (int i = 0; i < missingSlots; i++)
            {
                injectedSlots.Add(existingCapacity + i);
                System.Xml.Linq.XElement subContainer = new("SubContainer",
                    new System.Xml.Linq.XAttribute("capacity", "1"),
                    new System.Xml.Linq.XAttribute("maxstacksize", "1"));
                subContainer.Add(new System.Xml.Linq.XElement(quickContainable));
                element.Add(new ContentXElement(element.ContentPackage, subContainer));
            }
        }
    }
}
