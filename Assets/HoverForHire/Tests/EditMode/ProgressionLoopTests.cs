using System.Collections.Generic;
using NUnit.Framework;

namespace HoverForHire.Tests
{
    /// <summary>Certifications earned from drills, and the offer board's dealing rules.</summary>
    public sealed class ProgressionLoopTests
    {
        private static ContractDefinition Contract(string id, string pickup, string destination)
            => new ContractDefinition
            {
                Id = id, Title = id, Pickup = new ZoneDefinition { Id = pickup, Name = pickup },
                Destination = new ZoneDefinition { Id = destination, Name = destination }
            };

        private static int Drills(params int[] drills)
        {
            int mask = 0;
            foreach (int drill in drills) mask |= 1 << drill;
            return mask;
        }

        [Test]
        public void DrillsEarnTheCertificationsThatGateDemandingPads()
        {
            Assert.That(Certifications.Earned(0), Is.EqualTo(Certification.None));
            Assert.That(Certifications.Earned(Drills(6)), Is.EqualTo(Certification.Rooftop), "Precision landing earns rooftop work.");
            Assert.That(Certifications.Earned(Drills(TrainingSession.ConfinedArea)), Is.EqualTo(Certification.None), "Mountain needs both drills.");
            Assert.That(Certifications.Earned(Drills(TrainingSession.ConfinedArea, TrainingSession.HeavyLift)), Is.EqualTo(Certification.Mountain));
            Assert.That(Certifications.Earned(Drills(TrainingSession.Crosswind)), Is.EqualTo(Certification.Coastal));
            Assert.That(Certifications.Earned(Drills(TrainingSession.SettlingWithPower, TrainingSession.Autorotation)), Is.EqualTo(Certification.Emergency));
            int everything = (1 << TrainingSession.Names.Length) - 1;
            Assert.That(Certifications.Earned(everything), Is.EqualTo(Certification.Rooftop | Certification.Mountain | Certification.Coastal | Certification.Emergency));
            Assert.That(Certifications.Satisfies(Drills(6), Certification.None), Is.True, "Uncertified jobs are always open.");
            Assert.That(Certifications.Satisfies(Drills(6), Certification.Rooftop | Certification.Coastal), Is.False, "Every listed certification is needed.");
            Assert.That(Certifications.Requirement(Certification.Mountain), Is.EqualTo("Mountain: Confined area and Heavy lift drills"));
            Assert.That(Certifications.Describe(Certification.Coastal | Certification.Rooftop), Is.EqualTo("Rooftop + Coastal"));
        }

        [Test]
        public void OffersStartWhereTheAircraftIsAndNeverRepeat()
        {
            var contracts = new List<ContractDefinition>
            {
                Contract("a", "home", "town"), Contract("b", "town", "dock"), Contract("c", "dock", "yard"),
                Contract("d", "town", "clinic"), Contract("e", "yard", "ridge")
            };
            for (int seed = 0; seed < 50; seed++)
            {
                List<ContractDefinition> fromTown = OfferBoard.Deal(contracts, "town", seed);
                Assert.That(fromTown.Count, Is.EqualTo(OfferBoard.Size));
                Assert.That(fromTown[0].Pickup.Id, Is.EqualTo("town"), "The first offer needs no repositioning flight.");
                Assert.That(new HashSet<ContractDefinition>(fromTown).Count, Is.EqualTo(fromTown.Count), "Offers are distinct.");
            }
            Assert.That(OfferBoard.Deal(contracts, "town", 7), Is.EqualTo(OfferBoard.Deal(contracts, "town", 7)), "Same seed, same offers.");
            List<ContractDefinition> nowhere = OfferBoard.Deal(contracts, "lighthouse", 3);
            Assert.That(nowhere.Count, Is.EqualTo(OfferBoard.Size), "Without a local job the board still fills.");
            Assert.That(OfferBoard.Deal(contracts.GetRange(0, 2), "home", 1).Count, Is.EqualTo(2), "Fewer contracts, fewer offers.");
            Assert.That(OfferBoard.Deal(new List<ContractDefinition>(), "home", 1), Is.Empty);
        }

        [Test]
        public void ParIsAMinuteForTheTerminalsPlusCruiseAt25MetresPerSecond()
        {
            Assert.That(MissionDirector.ParSeconds(0f), Is.EqualTo(60f));
            Assert.That(MissionDirector.ParSeconds(390f), Is.EqualTo(75.6f).Within(0.01f),
                "Home to Town Green: the autopilot flies it in about 71 s.");
            Assert.That(MissionDirector.ParSeconds(-5f), Is.EqualTo(60f));
        }
    }
}
