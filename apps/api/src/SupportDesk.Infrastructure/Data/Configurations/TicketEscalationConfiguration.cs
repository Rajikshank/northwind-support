using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
namespace SupportDesk.Infrastructure.Data.Configurations;
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.EscalatedBy).HasMaxLength(100).IsRequired();
        b.Property(x => x.FromDueAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.ToDueAtUtc).HasColumnType("datetime2(7)");
        b.Property(x => x.EscalatedAtUtc).HasColumnType("datetime2(7)");
        b.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Agent>().WithMany().HasForeignKey(x => x.FromAgentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Agent>().WithMany().HasForeignKey(x => x.ToAgentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.TicketId, x.EscalatedAtUtc, x.Id }).IsDescending(false, true, true);
    }
}
