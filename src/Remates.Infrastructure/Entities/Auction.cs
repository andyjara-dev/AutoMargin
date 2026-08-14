namespace Remates.Infrastructure.Entities;

public enum BidResult { NotBid = 0, Won = 1, Lost = 2 }

public class AuctionHouse : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Comisión por defecto de este martillero. Puede sobrescribirse por remate.</summary>
    public decimal DefaultCommissionPct { get; set; }
    public bool CommissionHasVat { get; set; } = true;

    public decimal AdminFeeFixed { get; set; }
    public decimal StorageFeePerDay { get; set; }

    public string? TermsUrl { get; set; }

    public ICollection<Auction> Auctions { get; set; } = [];
}

public class Auction : AuditableEntity
{
    public long AuctionHouseId { get; set; }
    public AuctionHouse? AuctionHouse { get; set; }

    public required string Name { get; set; }
    public DateTimeOffset AuctionDate { get; set; }
    public string? Region { get; set; }
    public string? TermsUrl { get; set; }

    public ICollection<AuctionLot> Lots { get; set; } = [];
}

public class AuctionLot : AuditableEntity
{
    public long AuctionId { get; set; }
    public Auction? Auction { get; set; }

    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public string? LotNumber { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? DepositRequired { get; set; }
    public DateTimeOffset? ClosesAt { get; set; }

    public ICollection<Bid> Bids { get; set; } = [];
}

/// <summary>
/// Registro de una puja. Guarda el precio de adjudicación aunque perdamos: ese dato es el único
/// que permite saber después si nuestra puja máxima es demasiado conservadora.
/// </summary>
public class Bid : AuditableEntity
{
    public long AuctionLotId { get; set; }
    public AuctionLot? AuctionLot { get; set; }

    /// <summary>La puja máxima que autorizó el sistema en ese momento.</summary>
    public decimal MaxBidAuthorized { get; set; }

    /// <summary>Lo que efectivamente ofrecimos.</summary>
    public decimal? BidPlaced { get; set; }

    public BidResult Result { get; set; } = BidResult.NotBid;

    /// <summary>Precio al que se adjudicó el lote, lo hayamos ganado o no.</summary>
    public decimal? WinningPrice { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
    public string? Note { get; set; }
}
