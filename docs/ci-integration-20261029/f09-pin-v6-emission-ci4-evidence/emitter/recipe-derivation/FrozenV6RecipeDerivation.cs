using System;
using System.Linq;
using System.Text.Json;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;

internal static class FrozenV6RecipeDerivation
{
    private const uint Seed = 17320508u;

    private static string[] FortuneOptionIds =
    {
        "consumable.health_potion",
        "consumable.energy_tonic",
        "consumable.antidote",
        "consumable.bomb",
        "consumable.fortune_brew",
        "consumable.torch"
    };

    private static ExpeditionRouteCatalog CreateRouteCatalog()
    {
        return new ExpeditionRouteCatalog(new[]
        {
            CreateRouteSector("patio"),
            CreateRouteSector("archivo"),
            CreateRouteSector("forjas")
        }, "room.hub.rest", "room.final.arena");
    }

    private static ExpeditionRouteSectorCatalog CreateRouteSector(string sectorId)
    {
        return new ExpeditionRouteSectorCatalog(
            sectorId,
            System.Linq.Enumerable.Range(1, 5).Select(index => "room." + sectorId + ".c" + index),
            System.Linq.Enumerable.Range(1, 2).Select(index => "room." + sectorId + ".e" + index),
            "room." + sectorId + ".t",
            "room." + sectorId + ".p",
            "room." + sectorId + ".n",
            "room." + sectorId + ".guardian");
    }

    private static Guid FixedGuid(int seed)
    {
        var bytes = new byte[16];
        bytes[0] = (byte)seed;
        bytes[15] = 1;
        return new Guid(bytes);
    }

    private static object BuildFortuneDerivation()
    {
        var book = new FortuneOfferBook(
            ExpeditionRouteGenerator.GenerationVersion,
            Seed,
            FortuneOfferBook.HumanRaceId);
        var grantGuardian = book.GrantGuardianCharge("patio.guardian");
        FortuneOfferResult created = book.CreateOffer("patio.guardian", "patio.guardian", FortuneOptionIds);
        var firstReroll = book.Reroll(created.Offer.OfferId, new TransactionId(FixedGuid(21)));
        FortuneRerollResult reroll = book.Reroll(created.Offer.OfferId, new TransactionId(FixedGuid(22)));
        string selectedOptionId = reroll.Offer.OptionIds[0];
        var choose = book.Choose(created.Offer.OfferId, selectedOptionId);
        var claim = book.Claim(created.Offer.OfferId);
        var brew = book.GrantFortuneBrewCharge(
            ItemInstanceId.Parse(FixedGuid(23).ToString("N")),
            new TransactionId(FixedGuid(24)));
        return new
        {
            inputs = new
            {
                generationVersion = ExpeditionRouteGenerator.GenerationVersion,
                seed = Seed,
                raceId = FortuneOfferBook.HumanRaceId,
                guardianId = "patio.guardian",
                optionIds = FortuneOptionIds,
                transaction21 = FixedGuid(21).ToString("N"),
                transaction22 = FixedGuid(22).ToString("N"),
                itemInstance23 = FixedGuid(23).ToString("N"),
                transaction24 = FixedGuid(24).ToString("N")
            },
            operationStatuses = new
            {
                grantGuardianCharge = grantGuardian.Status.ToString(),
                createOffer = created.Status.ToString(),
                firstReroll = firstReroll.Status.ToString(),
                secondReroll = reroll.Status.ToString(),
                choose = choose.Status.ToString(),
                claim = claim.Status.ToString(),
                grantFortuneBrewCharge = brew.Status.ToString()
            },
            finalSnapshot = book.CreateSnapshot()
        };
    }

    public static void Main()
    {
        ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(Seed, CreateRouteCatalog());
        var derivation = new
        {
            procedure = "Core-only deterministic derivation; no UnityEngine, JsonUtility, save DTO, or fixture/golden access.",
            routeInputs = new
            {
                seed = Seed,
                sectors = new[] { "patio", "archivo", "forjas" },
                combatRoomsPerSector = 5,
                explorationRoomsPerSector = 2,
                hubNodeId = "room.hub.rest",
                finalNodeId = "room.final.arena"
            },
            route = new
            {
                generationVersion = route.GenerationVersion,
                currentNodeId = route.MandatoryNodeIds[0],
                mandatoryNodeIds = route.MandatoryNodeIds,
                canonicalForm = route.CanonicalForm
            },
            fortune = BuildFortuneDerivation()
        };
        Console.WriteLine(JsonSerializer.Serialize(derivation, new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true
        }));
    }
}
