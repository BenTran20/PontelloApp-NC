using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace PontelloApp.Models
{
    public class Variant :Auditable
    {
        public int Id { get; set; }

        [StringLength(100)]
        public string Name { get; set; }   

        [StringLength(100)]
        public string Value { get; set; }

        public int ProductVariantId { get; set; }
        public ProductVariant? ProductVariant { get; set; }

       
    }
}
