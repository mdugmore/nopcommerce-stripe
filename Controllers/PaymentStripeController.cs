using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Stripe.Models;
using Nop.Plugin.Payments.Stripe.Validators;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.Stripe.Controllers
{
    public class PaymentStripeController : BasePaymentController
    {
        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;

        public PaymentStripeController(IWorkContext workContext,
            IStoreService storeService, 
            ISettingService settingService, 
            ILocalizationService localizationService)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._localizationService = localizationService;
        }
        
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var stripePaymentSettings = _settingService.LoadSetting<StripePaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.TransactModeId = Convert.ToInt32(stripePaymentSettings.TransactMode);
            model.AdditionalFee = stripePaymentSettings.AdditionalFee;
            model.AdditionalFeePercentage = stripePaymentSettings.AdditionalFeePercentage;
            model.TransactModeValues = stripePaymentSettings.TransactMode.ToSelectList();
            model.ApiKey = stripePaymentSettings.ApiKey;

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(stripePaymentSettings, x => x.TransactMode, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(stripePaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(stripePaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.ApiKey_OverrideForStore = _settingService.SettingExists(stripePaymentSettings, x => x.ApiKey, storeScope);
            }

            return View("~/Plugins/Payments.Stripe/Views/PaymentStripe/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var stripePaymentSettings = _settingService.LoadSetting<StripePaymentSettings>(storeScope);

            //save settings
            stripePaymentSettings.TransactMode = TransactMode.Authorize;
            stripePaymentSettings.AdditionalFee = model.AdditionalFee;
            stripePaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            stripePaymentSettings.ApiKey = model.ApiKey;
            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            if (model.TransactModeId_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(stripePaymentSettings, x => x.TransactMode, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(stripePaymentSettings, x => x.TransactMode, storeScope);

            if (model.AdditionalFee_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(stripePaymentSettings, x => x.AdditionalFee, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(stripePaymentSettings, x => x.AdditionalFee, storeScope);

            if (model.AdditionalFeePercentage_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(stripePaymentSettings, x => x.AdditionalFeePercentage, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(stripePaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            if (model.ApiKey_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(stripePaymentSettings, x => x.ApiKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(stripePaymentSettings, x => x.ApiKey, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();
            
            //CC types
            model.CreditCardTypes.Add(new SelectListItem
                {
                    Text = "Visa",
                    Value = "Visa",
                });
            model.CreditCardTypes.Add(new SelectListItem
            {
                Text = "Master card",
                Value = "MasterCard",
            });
            model.CreditCardTypes.Add(new SelectListItem
            {
                Text = "Amex",
                Value = "Amex",
            });
            
            //years
            for (int i = 0; i < 15; i++)
            {
                string year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (int i = 1; i <= 12; i++)
            {
                string text = (i < 10) ? "0" + i : i.ToString();
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }

            //set postback values
            var form = this.Request.Form;
            model.CardholderName = form["CardholderName"];
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            var selectedCcType = model.CreditCardTypes.FirstOrDefault(x => x.Value.Equals(form["CreditCardType"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedCcType != null)
                selectedCcType.Selected = true;
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x => x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedMonth != null)
                selectedMonth.Selected = true;
            var selectedYear = model.ExpireYears.FirstOrDefault(x => x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));
            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("~/Plugins/Payments.Stripe/Views/PaymentStripe/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    warnings.Add(error.ErrorMessage);
            return warnings;
        }

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();
            paymentInfo.CreditCardType = form["CreditCardType"];
            paymentInfo.CreditCardName = form["CardholderName"];
            paymentInfo.CreditCardNumber = form["CardNumber"];
            paymentInfo.CreditCardExpireMonth = int.Parse(form["ExpireMonth"]);
            paymentInfo.CreditCardExpireYear = int.Parse(form["ExpireYear"]);
            paymentInfo.CreditCardCvv2 = form["CardCode"];
            return paymentInfo;
        }
    }
}