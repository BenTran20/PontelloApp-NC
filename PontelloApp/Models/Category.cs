using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PontelloApp.Models
{
    public class Category : Auditable
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "The Category Name is required.")]
        [StringLength(100, ErrorMessage = "Category Name cannot be more than 100 characters long.")]
        public string Name { get; set; } = "";

        public ICollection<Product> Products { get; set; } = new HashSet<Product>();

        //self-reference
        [Display(Name="Parent Category")]
        public int? ParentCategoryID { get; set; }

        [ForeignKey("ParentCategoryID")]
        public Category? ParentCategory { get; set; }

        public ICollection<Category> SubCategories { get; set; } = new HashSet<Category>();
        public string? FullCategory
        {
            get
            {
                var names = new List<string>();
                Category? current = this;

                while (current != null)
                {
                    names.Add(current.Name);
                    current = current.ParentCategory;
                }

                names.Reverse();
                return string.Join(" > ", names);
            }
        }


    }
}
