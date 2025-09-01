
namespace Database.Entity;

    public class Process
    {
        public int? Id { get; set; }
        
        public bool? Processed { get; set; }
    
        public DateTime ProcessedDateTime { get; set; }
    }
