using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace jett_exchange_backend.Models;

public class NormalJettTicket
{
    
    [Key]
    public int Id { get; set; }
    
    [MaxLength(45)]    
    public required string OriginalOwnerName { get; set; }
    [MaxLength(12)]   
    public required string OriginalOwnerPassportNumber { get; set; }

    public DateTime Date { get; set; }
    
    [Column(TypeName = "decimal(3,2)")]
    public required decimal Price { get; set; }
    public required int NumberOfBags { get; set; }
    public required decimal TotalPrice { get; set; }
}
