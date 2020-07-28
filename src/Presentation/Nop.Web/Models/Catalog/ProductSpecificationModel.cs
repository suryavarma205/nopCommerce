using System.Collections.Generic;

namespace Nop.Web.Models.Catalog
{
    /// <summary>
    /// Represents a product specification model
    /// </summary>
    public partial class ProductSpecificationModel
    {
        #region Properties

        /// <summary>
        /// Gets or sets the specification attribute models without groups
        /// </summary>
        public IList<ProductSpecificationAttributeModel> NonGroupedAttributes { get; set; }

        /// <summary>
        /// Gets or sets the grouped specification attribute models
        /// </summary>
        public IList<ProductSpecificationAttributeGroupModel> GroupedAttributes { get; set; }

        #endregion

        #region Ctor

        public ProductSpecificationModel()
        {
            NonGroupedAttributes = new List<ProductSpecificationAttributeModel>();
            GroupedAttributes = new List<ProductSpecificationAttributeGroupModel>();
        }

        #endregion
    }
}