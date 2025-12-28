using System;

namespace Backend.Data;

public class LeaderboardRow
{
    public int Id { get; set; }

    public string Season { get; set; } = "";
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";

    public decimal Prize { get; set; }
    public decimal Bet { get; set; }

    public int Rank { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
