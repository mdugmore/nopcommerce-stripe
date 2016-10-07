using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.Stripe.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        public int TransactModeId { get; set; }
        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.TransactMode")]
        public SelectList TransactModeValues { get; set; }
        public bool TransactModeId_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Stripe.Fields.ApiKey")]
        public string ApiKey { get; set; }
        public bool ApiKey_OverrideForStore { get; set; }
    }
}