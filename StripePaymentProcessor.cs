using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Plugin.Payments.Stripe.Controllers;
using Stripe;

namespace Nop.Plugin.Payments.Stripe
{
    public class StripePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly StripePaymentSettings _stripePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ICurrencyService _currencyService;

        #endregion

        #region Ctor

        public StripePaymentProcessor(StripePaymentSettings stripePaymentSettings,
            ISettingService settingService, IOrderTotalCalculationService orderTotalCalculationService,
            ICurrencyService currencyService)
        {
            this._stripePaymentSettings = stripePaymentSettings;
            this._settingService = settingService;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._currencyService = currencyService;
        }

        #endregion

        #region Methods
        
        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            
            result.AllowStoringCreditCardNumber = false;

            StripeCreditCardOptions cardOptions = new StripeCreditCardOptions()
            {
                Number = processPaymentRequest.CreditCardNumber,
                ExpirationYear = processPaymentRequest.CreditCardExpireYear.ToString(),
                ExpirationMonth = processPaymentRequest.CreditCardExpireMonth.ToString(),
                Cvc = processPaymentRequest.CreditCardCvv2,
                Name = processPaymentRequest.CustomerId.ToString()
            };
            string token = CreateToken(cardOptions);
            int paymentAmount = 0;

            if(_stripePaymentSettings.AdditionalFeePercentage && _stripePaymentSettings.AdditionalFee > 0)
            {
                decimal additionalFee = processPaymentRequest.OrderTotal * (_stripePaymentSettings.AdditionalFee / 100);
                paymentAmount = (int)(processPaymentRequest.OrderTotal * 100) + (int)(additionalFee * 100); //convert to cents/pence
            } else if (!_stripePaymentSettings.AdditionalFeePercentage && _stripePaymentSettings.AdditionalFee > 0)
            {
                paymentAmount = (int)(processPaymentRequest.OrderTotal * 100) + (int)(_stripePaymentSettings.AdditionalFee * 100);
            }
            else
            {
                paymentAmount = (int)(processPaymentRequest.OrderTotal * 100);
            }
            
            StripeChargeCreateOptions chargeOptions = new StripeChargeCreateOptions()
            {
                Amount = paymentAmount,
                Currency = "gbp",
                Description = processPaymentRequest.OrderGuid.ToString()

            };

            if (ChargeCard(token, chargeOptions))
            {
                result.NewPaymentStatus = PaymentStatus.Paid;
            }
            else
            {
                result.AddError("Failed");

            }
            //switch (_stripePaymentSettings.TransactMode)
            //{
            //    //case TransactMode.Pending:
            //    //    result.NewPaymentStatus = PaymentStatus.Pending;
            //    //    break;
            //    case TransactMode.Authorize:
            //        result.NewPaymentStatus = PaymentStatus.Authorized;
            //        break;
            //    case TransactMode.AuthorizeAndCapture:
            //        result.NewPaymentStatus = PaymentStatus.Paid;
            //        break;
            //    default:
            //        {
            //            result.AddError("Not supported transaction type");
            //            return result;
            //        }
            //}

            return result;
        }

        private string CreateToken(StripeCreditCardOptions card)
        {
            var myToken = new StripeTokenCreateOptions
            {
                Card = card
            };

            var tokenService = new StripeTokenService();
            tokenService.ApiKey = _stripePaymentSettings.ApiKey;
            var stripeToken = tokenService.Create(myToken);
            return stripeToken.Id;
        }

        private bool ChargeCard(string tokenId, StripeChargeCreateOptions chargeOptions)
        {
            // setting up the card
            chargeOptions.Source = new StripeSourceOptions()
            {
                // set this property if using a token
                TokenId = tokenId

            };

            // set this property if using a customer
            //    myCharge.CustomerId = *customerId*;

            // set this if you have your own application fees (you must have your application configured first within Stripe)
            //    myCharge.ApplicationFee = 25;

            // (not required) set this to false if you don't want to capture the charge yet - requires you call capture later
            chargeOptions.Capture = true;

            var chargeService = new StripeChargeService();
            chargeService.ApiKey = _stripePaymentSettings.ApiKey;
            StripeCharge stripeCharge = chargeService.Create(chargeOptions);
            return stripeCharge.Captured.HasValue ? stripeCharge.Captured.Value : false;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService,  cart,
                _stripePaymentSettings.AdditionalFee, _stripePaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            result.AllowStoringCreditCardNumber = true;
            switch (_stripePaymentSettings.TransactMode)
            {
                //case TransactMode.Pending:
                //    result.NewPaymentStatus = PaymentStatus.Pending;
                //    break;
                case TransactMode.Authorize:
                    result.NewPaymentStatus = PaymentStatus.Authorized;
                    break;
                case TransactMode.AuthorizeAndCapture:
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    break;
                default:
                    {
                        result.AddError("Not supported transaction type");
                        return result;
                    }
            }
            
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            //always success
            return new CancelRecurringPaymentResult();
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentStripe";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Stripe.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentStripe";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.Stripe.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentStripeController);
        }

        public override void Install()
        {
            //settings
            var settings = new StripePaymentSettings
            {
                TransactMode = TransactMode.Authorize
                
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.ApiKey", "Public Api Key");
            

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<StripePaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.TransactMode");
            this.DeletePluginLocaleResource("Plugins.Payments.Manual.Fields.TransactMode.Hint");
            
            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.Manual;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
}
