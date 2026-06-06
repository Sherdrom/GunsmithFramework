GunsmithFramework = GunsmithFramework or {}

local Gunsmith = GunsmithFramework
local Core = Gunsmith.Core
local Stats = {}
Gunsmith.Stats = Stats

Stats.Keys = {
    "Ergonomics",
    "ElectricalSkillBonus",
    "HelmSkillBonus",
    "MechanicalSkillBonus",
    "MedicalSkillBonus",
    "WeaponsSkillBonus",
    "HelmSkillOverride",
    "MedicalSkillOverride",
    "WeaponsSkillOverride",
    "ElectricalSkillOverride",
    "MechanicalSkillOverride",
    "MaximumHealthMultiplier",
    "MovementSpeed",
    "WalkingSpeed",
    "SwimmingSpeed",
    "PropulsionSpeed",
    "BuffDurationMultiplier",
    "DebuffDurationMultiplier",
    "MedicalItemEffectivenessMultiplier",
    "FlowResistance",
    "AttackMultiplier",
    "TeamAttackMultiplier",
    "RangedAttackSpeed",
    "RangedAttackMultiplier",
    "TurretAttackSpeed",
    "TurretPowerCostReduction",
    "TurretChargeSpeed",
    "MeleeAttackSpeed",
    "MeleeAttackMultiplier",
    "RangedSpreadReduction",
    "RepairSpeed",
    "MechanicalRepairSpeed",
    "ElectricalRepairSpeed",
    "DeconstructorSpeedMultiplier",
    "RepairToolStructureRepairMultiplier",
    "RepairToolStructureDamageMultiplier",
    "RepairToolDeattachTimeMultiplier",
    "MaxRepairConditionMultiplierMechanical",
    "MaxRepairConditionMultiplierElectrical",
    "IncreaseFabricationQuality",
    "GeneticMaterialRefineBonus",
    "GeneticMaterialTaintedProbabilityReductionOnCombine",
    "SkillGainSpeed",
    "ExtraLevelGain",
    "HelmSkillGainSpeed",
    "WeaponsSkillGainSpeed",
    "MedicalSkillGainSpeed",
    "ElectricalSkillGainSpeed",
    "MechanicalSkillGainSpeed",
    "MedicalItemApplyingMultiplier",
    "BuffItemApplyingMultiplier",
    "PoisonMultiplier",
    "TinkeringDuration",
    "TinkeringStrength",
    "TinkeringDamage",
    "ReputationGainMultiplier",
    "ReputationLossMultiplier",
    "MissionMoneyGainMultiplier",
    "ExperienceGainMultiplier",
    "MissionExperienceGainMultiplier",
    "ExtraMissionCount",
    "ExtraSpecialSalesCount",
    "StoreSellMultiplier",
    "StoreBuyMultiplierAffiliated",
    "StoreBuyMultiplier",
    "ShipyardBuyMultiplierAffiliated",
    "ShipyardBuyMultiplier",
    "MaxAttachableCount",
    "ExplosionRadiusMultiplier",
    "ExplosionDamageMultiplier",
    "FabricationSpeed",
    "BallastFloraDamageMultiplier",
    "HoldBreathMultiplier",
    "Apprenticeship",
    "CPRBoost",
    "LockedTalents",
    "HireCostMultiplier",
    "InventoryExtraStackSize",
    "SoundRangeMultiplier",
    "SightRangeMultiplier",
    "DualWieldingPenaltyReduction",
    "NaturalMeleeAttackMultiplier"
}

function Stats.Empty()
    local result = {}
    for _, key in ipairs(Stats.Keys) do
        result[key] = 0
    end
    return result
end

function Stats.PartStats(part)
    local result = Stats.Empty()
    if not part then return result end

    if type(part.stats) == "table" then
        for _, key in ipairs(Stats.Keys) do
            if type(part.stats[key]) == "number" then
                result[key] = part.stats[key]
            end
        end
    end
    return result
end

function Stats.Add(target, source)
    for _, key in ipairs(Stats.Keys) do
        target[key] = (target[key] or 0) + (source[key] or 0)
    end
    return target
end

function Stats.SumSelection(selection)
    local result = {}
    if type(selection) ~= "table" then return result end
    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        local part = Core.GetPart(selection[path])
        if part and type(part.stats) == "table" then
            for _, key in ipairs(Stats.Keys) do
                local value = part.stats[key]
                if type(value) == "number" and value ~= 0 then
                    result[key] = (result[key] or 0) + value
                end
            end
        end
    end
    return result
end

function Stats.Encode(stats, separator)
    local values = {}
    local source = stats or Stats.Empty()
    for _, key in ipairs(Stats.Keys) do
        local value = source[key] or 0
        if value ~= 0 then
            table.insert(values, key .. "=" .. string.format("%.4f", value))
        end
    end
    return table.concat(values, separator or ",")
end

function Stats.ManagedItemIdentifiers(selection)
    local ids = {}
    if type(selection) ~= "table" then return ids end

    for _, path in ipairs(Core.SortedSelectionPaths(selection)) do
        local part = Core.GetPart(selection[path])
        local itemId = part and part.item and part.item.identifier or nil
        if type(itemId) == "string" and itemId ~= "" then
            table.insert(ids, itemId)
        end
    end
    return ids
end
