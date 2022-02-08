﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;

namespace FantasyCritic.Lib.Domain.LeagueActions
{
    public class SucceededPickupBid : IProcessedBid
    {
        public SucceededPickupBid(PickupBid pickupBid, int slotNumber, string outcome, SystemWideValues systemWideValues, LocalDate currentDate)
        {
            PickupBid = pickupBid;
            SlotNumber = slotNumber;
            Outcome = outcome;
            ProjectedPointsAtTimeOfBid = PickupBid.Publisher.GetProjectedFantasyPoints(systemWideValues, false, currentDate, false);
        }

        public PickupBid PickupBid { get; }
        public int SlotNumber { get; }
        public string Outcome { get; }
        public decimal ProjectedPointsAtTimeOfBid { get; }
    }
}