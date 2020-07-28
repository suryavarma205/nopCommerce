using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using FluentAssertions;
using Nop.Core.ComponentModel;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Discounts;
using Nop.Services.Orders;
using Nop.Services.Tests.Payments;
using Nop.Services.Tests.Shipping;
using NUnit.Framework;

namespace Nop.Services.Tests.Orders
{
    [TestFixture]
    public class OrderTotalCalculationServiceTests : ServiceTest
    {
        private IOrderTotalCalculationService orderTotalCalcService;
        private IProductService productService;
        private ICustomerService customerService;
        private IDiscountService discountService;
        private TaxSettings taxSettings;
        private ISettingService settingService;
        private IShoppingCartService shoppingCartService;
        private ShoppingCartSettings shoppingCartSettings;
        private RewardPointsSettings rewardPointsSettings;

        private Discount discount;
        private Customer customer;

        #region Utilities

        private ShoppingCartItem CreateTestShopCartItem(decimal productPrice, int quantity = 1)
        {
            //shopping cart
            var product = new Product
            {
                Name = "Product name 1",
                Price = productPrice,
                CustomerEntersPrice = false,
                Published = true,
                //set HasTierPrices property
                HasTierPrices = true
            };

            productService.InsertProduct(product);

            var shoppingCartItem = new ShoppingCartItem
            {
                CustomerId = customer.Id,
                ProductId = product.Id,
                Quantity = quantity
            };

            return shoppingCartItem;
        }

        private List<ShoppingCartItem> ShoppingCart
        {
            get
            {
                var sci1 = new ShoppingCartItem
                {
                    ProductId = productService.GetProductBySku("FR_451_RB").Id,
                    Quantity = 2
                };
                var sci2 = new ShoppingCartItem
                {
                    ProductId = productService.GetProductBySku("FIRST_PRP").Id,
                    Quantity = 3
                };

                var cart = new List<ShoppingCartItem> { sci1, sci2 };
                cart.ForEach(sci => sci.CustomerId = customer.Id);

                return cart;
            }
        }

        #endregion

        [SetUp]
        public void SetUp()
        {
            TypeDescriptor.AddAttributes(typeof(List<int>),
                new TypeConverterAttribute(typeof(GenericListTypeConverter<int>)));
            TypeDescriptor.AddAttributes(typeof(List<string>),
                new TypeConverterAttribute(typeof(GenericListTypeConverter<string>)));

            settingService = GetService<ISettingService>();

            var shippingSettings = GetService<ShippingSettings>();
            shippingSettings.ActiveShippingRateComputationMethodSystemNames.Add("FixedRateTestShippingRateComputationMethod");
            taxSettings = GetService<TaxSettings>();
            taxSettings.ActiveTaxProviderSystemName = "FixedTaxRateTest";
            taxSettings.ShippingIsTaxable = true;
            settingService.SaveSetting(shippingSettings);
            settingService.SaveSetting(taxSettings);

            orderTotalCalcService = GetService<IOrderTotalCalculationService>();
            productService = GetService<IProductService>();
            customerService = GetService<ICustomerService>();
            discountService = GetService<IDiscountService>();
            shoppingCartService = GetService<IShoppingCartService>();

            shoppingCartSettings = GetService<ShoppingCartSettings>();

            rewardPointsSettings = GetService<RewardPointsSettings>();

            discount = new Discount
            {
                Name = "Discount 1",
                DiscountType = DiscountType.AssignedToOrderSubTotal,
                DiscountAmount = 3,
                DiscountLimitation = DiscountLimitationType.Unlimited
            };

            customer = customerService.GetCustomerByEmail("test@nopCommerce.com");

            GetService<IGenericAttributeService>().SaveAttribute(customer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, "Payments.TestMethod", 1);
        }

        [TearDown]
        public void TearDown()
        {
            var settingService = GetService<ISettingService>();

            var shippingSettings = GetService<ShippingSettings>();
            shippingSettings.ActiveShippingRateComputationMethodSystemNames.Clear();

            taxSettings.PaymentMethodAdditionalFeeIsTaxable = false;
            taxSettings.ActiveTaxProviderSystemName = string.Empty;
            taxSettings.ShippingIsTaxable = false;
            settingService.SaveSetting(shippingSettings);
            settingService.SaveSetting(taxSettings);

            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            product = productService.GetProductBySku("FIRST_PRP");
            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            GetService<IGenericAttributeService>().SaveAttribute<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, null, 1);
            
            foreach (var item in GetService<IRepository<Discount>>().Table.Where(d => d.Name == "Discount 1").ToList()) 
                discountService.DeleteDiscount(item);

            productService.DeleteProducts(GetService<IRepository<Product>>().Table.Where(p => p.Name == "Product name 1").ToList());
        }

        [Test]
        public void CanGetShoppingCartSubTotalExcludingTax()
        {
            //10% - default tax rate
            orderTotalCalcService.GetShoppingCartSubTotal(ShoppingCart, false,
                out var discountAmount, out var appliedDiscounts,
                out var subTotalWithoutDiscount, out var subTotalWithDiscount, out var taxRates);
            discountAmount.Should().Be(0);
            appliedDiscounts.Count.Should().Be(0);
            subTotalWithoutDiscount.Should().Be(207M);
            subTotalWithDiscount.Should().Be(207M);
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(20.7M);
        }

        [Test]
        public void CanGetShoppingCartSubTotalIncludingTax()
        {
            orderTotalCalcService.GetShoppingCartSubTotal(ShoppingCart, true,
                out var discountAmount, out var appliedDiscounts,
                out var subTotalWithoutDiscount, out var subTotalWithDiscount, out var taxRates);
            discountAmount.Should().Be(0);
            appliedDiscounts.Count.Should().Be(0);
            subTotalWithoutDiscount.Should().Be(227.7M);
            subTotalWithDiscount.Should().Be(227.7M);
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(20.7M);
        }

        [Test]
        public void CanGetShoppingCartSubtotalDiscountExcludingTax()
        {
            discountService.InsertDiscount(discount);

            //10% - default tax rate
            orderTotalCalcService.GetShoppingCartSubTotal(ShoppingCart, false,
                out var discountAmount, out var appliedDiscounts,
                out var subTotalWithoutDiscount, out var subTotalWithDiscount, out var taxRates);

            discountService.DeleteDiscount(discount);

            discountAmount.Should().Be(3);
            appliedDiscounts.Count.Should().Be(1);
            appliedDiscounts.First().Name.Should().Be("Discount 1");
            subTotalWithoutDiscount.Should().Be(207M);
            subTotalWithDiscount.Should().Be(204M);
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(20.4M);
        }

        [Test]
        public void CanGetShoppingCartSubtotalDiscountIncludingTax()
        {
            discountService.InsertDiscount(discount);

            orderTotalCalcService.GetShoppingCartSubTotal(ShoppingCart, true,
                out var discountAmount, out var appliedDiscounts,
                out var subTotalWithoutDiscount, out var subTotalWithDiscount,
                out var taxRates);

            discountService.DeleteDiscount(discount);

            //The comparison test failed before, because of a very tiny number difference.
            //discountAmount.ShouldEqual(3.3);
            (Math.Round(discountAmount, 10) == 3.3M).Should().BeTrue();
            appliedDiscounts.Count.Should().Be(1);
            appliedDiscounts.First().Name.Should().Be("Discount 1");
            subTotalWithoutDiscount.Should().Be(227.7M);
            subTotalWithDiscount.Should().Be(224.4M);
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(20.4M);
        }

        [Test]
        public void CanGetShoppingCartItemAdditionalShippingCharge()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 21.25M;
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);
            var additionalShippingCharge = orderTotalCalcService.GetShoppingCartAdditionalShippingCharge(ShoppingCart);
            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);
            additionalShippingCharge.Should().Be(42.5M);
        }

        [Test]
        public void ShippingShouldBeFreeWhenAllShoppingCartItemsAreMarkedAsFreeShipping()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            productService.GetProductBySku("FIRST_PRP");
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            orderTotalCalcService.IsFreeShipping(ShoppingCart).Should().BeTrue();
        }

        [Test]
        public void ShippingShouldNotBeFreeWhenSomeOfShoppingCartItemsAreNotMarkedAsFreeShipping()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);
            var isFreeShipping = orderTotalCalcService.IsFreeShipping(ShoppingCart);
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);
            isFreeShipping.Should().BeFalse();
        }

        [Test]
        public void ShippingShouldBeFreeWhenCustomerIsInRoleWithFreeShipping()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);
            var role = customerService.GetCustomerRoleBySystemName(NopCustomerDefaults.AdministratorsRoleName);
            role.FreeShipping = true;
            customerService.UpdateCustomerRole(role);
            var isFreeShipping = orderTotalCalcService.IsFreeShipping(ShoppingCart);
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);
            role.FreeShipping = false;
            customerService.UpdateCustomerRole(role);
            isFreeShipping.Should().BeTrue();
        }

        [Test]
        public void CanGetShippingTotalWithFixedShippingRateExcludingTax()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 21.25M;
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            var shipping =
                orderTotalCalcService.GetShoppingCartShippingTotal(ShoppingCart, false, out var taxRate,
                    out var appliedDiscounts);

            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            shipping.Should().NotBeNull();
            //10 - default fixed shipping rate, 42.5 - additional shipping change
            shipping.Should().Be(52.5M);
            appliedDiscounts.Count.Should().Be(0);
            //10 - default fixed tax rate
            taxRate.Should().Be(10);
        }

        [Test]
        public void CanGetShippingTotalWithFixedShippingRateIncludingTax()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 21.25M;
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            var shipping =
                orderTotalCalcService.GetShoppingCartShippingTotal(ShoppingCart, true, out var taxRate,
                    out var appliedDiscounts);

            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            shipping.Should().NotBeNull();
            //10 - default fixed shipping rate, 42.5 - additional shipping change
            shipping.Should().Be(57.75M);
            appliedDiscounts.Count.Should().Be(0);
            //10 - default fixed tax rate
            taxRate.Should().Be(10);
        }

        [Test]
        public void CanGetShippingTotalDiscountExcludingTax()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 21.25M;
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            discount.DiscountType = DiscountType.AssignedToShipping;
            discountService.InsertDiscount(discount);

            var shipping =
                orderTotalCalcService.GetShoppingCartShippingTotal(ShoppingCart, false, out var taxRate,
                    out var appliedDiscounts);

            discountService.DeleteDiscount(discount);
            discount.DiscountType = DiscountType.AssignedToOrderSubTotal;
            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            appliedDiscounts.Count.Should().Be(1);
            appliedDiscounts.First().Name.Should().Be("Discount 1");
            shipping.Should().NotBeNull();
            //10 - default fixed shipping rate, 42.5 - additional shipping change, -3 - discount
            shipping.Should().Be(49.5M);
            //10 - default fixed tax rate
            taxRate.Should().Be(10);
        }

        [Test]
        public void CanGetShippingTotalDiscountIncludingTax()
        {
            var product = productService.GetProductBySku("FR_451_RB");
            product.AdditionalShippingCharge = 21.25M;
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            discount.DiscountType = DiscountType.AssignedToShipping;
            discountService.InsertDiscount(discount);

            var shipping =
                orderTotalCalcService.GetShoppingCartShippingTotal(ShoppingCart, true, out var taxRate,
                    out var appliedDiscounts);

            discountService.DeleteDiscount(discount);
            discount.DiscountType = DiscountType.AssignedToOrderSubTotal;
            product.AdditionalShippingCharge = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            appliedDiscounts.Count.Should().Be(1);
            appliedDiscounts.First().Name.Should().Be("Discount 1");
            shipping.Should().NotBeNull();
            //10 - default fixed shipping rate, 42.5 - additional shipping change, -3 - discount
            shipping.Should().Be(54.45M);
            //10 - default fixed tax rate
            taxRate.Should().Be(10);
        }

        [Test]
        public void CanGetTaxTotal()
        {
            //207 - items, 10 - shipping (fixed), 20 - payment fee

            TestPaymentMethod.AdditionalHandlingFee = 20M;
            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            //1. shipping is taxable, payment fee is taxable
            taxSettings.ShippingIsTaxable = true;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = true;

            settingService.SaveSetting(taxSettings);

            GetService<IOrderTotalCalculationService>().GetTaxTotal(ShoppingCart, out var taxRates).Should().Be(23.7M);
            taxRates.Should().NotBeNull();
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(23.7M);

            //2. shipping is taxable, payment fee is not taxable
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = false;
            settingService.SaveSetting(taxSettings);

            GetService<IOrderTotalCalculationService>().GetTaxTotal(ShoppingCart, out taxRates).Should().Be(21.7M);
            taxRates.Should().NotBeNull();
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(21.7M);

            //3. shipping is not taxable, payment fee is taxable
            taxSettings.ShippingIsTaxable = false;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = true;
            settingService.SaveSetting(taxSettings);

            GetService<IOrderTotalCalculationService>().GetTaxTotal(ShoppingCart, out taxRates).Should().Be(22.7M);
            taxRates.Should().NotBeNull();
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(22.7M);

            //4. shipping is not taxable, payment fee is not taxable
            taxSettings.ShippingIsTaxable = false;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = false;
            settingService.SaveSetting(taxSettings);

            GetService<IOrderTotalCalculationService>().GetTaxTotal(ShoppingCart, out taxRates).Should().Be(20.7M);
            taxRates.Should().NotBeNull();
            taxRates.Count.Should().Be(1);
            taxRates.ContainsKey(10).Should().BeTrue();
            taxRates[10].Should().Be(20.7M);

            TestPaymentMethod.AdditionalHandlingFee = 0M;
            product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);
        }

        [Test]
        public void CanGetShoppingCartTotalWithoutShippingRequired()
        {
            //shipping is taxable, payment fee is taxable
            taxSettings.ShippingIsTaxable = true;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = true;
            settingService.SaveSetting(taxSettings);

            TestPaymentMethod.AdditionalHandlingFee = 20M;

            //207 - items, 20 - payment fee, 22.7 - tax
            orderTotalCalcService.GetShoppingCartTotal(ShoppingCart, out _, out _, out _, out _, out _)
                .Should().Be(249.7M);

            TestPaymentMethod.AdditionalHandlingFee = 0M;
        }

        [Test]
        public void CanGetShoppingCartTotalWithShippingRequired()
        {
            //shipping is taxable, payment fee is taxable
            taxSettings.ShippingIsTaxable = true;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = true;

            settingService.SaveSetting(taxSettings);

            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            TestPaymentMethod.AdditionalHandlingFee = 20M;

            //207 - items, 10 - shipping (fixed), 20 - payment fee, 23.7 - tax
            orderTotalCalcService.GetShoppingCartTotal(ShoppingCart, out _, out _, out _, out _, out _)
                .Should().Be(260.7M);

            TestPaymentMethod.AdditionalHandlingFee = 0M;
            product.IsFreeShipping = true;
            productService.UpdateProduct(product);
        }

        [Test]
        public void CanGetShoppingCartItemUnitprice()
        {
            shoppingCartService.GetUnitPrice(ShoppingCart[0]).Should().Be(new decimal(27.0));
        }

        [Test]
        public void CanGetShoppingCartItemSubtotal()
        {
            shoppingCartService.GetSubTotal(ShoppingCart[0]).Should().Be(new decimal(54.0));
        }

        [Test]
        [TestCase(12.00009, 12.00)]
        [TestCase(12.119, 12.12)]
        [TestCase(12.115, 12.12)]
        [TestCase(12.114, 12.11)]
        public void TestGetUnitPriceWhenRoundPricesDuringCalculationIsTruePriceMustBeRounded(decimal inputPrice, decimal expectedPrice)
        {
            // arrange
            var shoppingCartItem = CreateTestShopCartItem(inputPrice);

            // act
            shoppingCartSettings.RoundPricesDuringCalculation = true;
            settingService.SaveSetting(shoppingCartSettings);

            var resultPrice = GetService<IShoppingCartService>().GetUnitPrice(shoppingCartItem);

            // assert
            resultPrice.Should().Be(expectedPrice);
        }

        [Test]
        [TestCase(12.00009, 12.00009)]
        [TestCase(12.119, 12.119)]
        [TestCase(12.115, 12.115)]
        [TestCase(12.114, 12.114)]
        public void TestGetUnitPriceWhenNotRoundPricesDuringCalculationIsFalsePriceMustNotBeRounded(decimal inputPrice, decimal expectedPrice)
        {
            // arrange            
            var shoppingCartItem = CreateTestShopCartItem(inputPrice);

            // act
            shoppingCartSettings.RoundPricesDuringCalculation = false;
            settingService.SaveSetting(shoppingCartSettings);

            var resultPrice = GetService<IShoppingCartService>().GetUnitPrice(shoppingCartItem);

            // assert
            resultPrice.Should().Be(expectedPrice);
        }

        [Test]
        public void CanGetShoppingCartTotalDiscount()
        {
            discount.DiscountType = DiscountType.AssignedToOrderTotal;

            discountService.InsertDiscount(discount);

            //shipping is taxable, payment fee is taxable
            taxSettings.ShippingIsTaxable = true;
            taxSettings.PaymentMethodAdditionalFeeIsTaxable = true;

            settingService.SaveSetting(taxSettings);

            TestPaymentMethod.AdditionalHandlingFee = 20M;

            var product = productService.GetProductBySku("FR_451_RB");
            product.IsFreeShipping = false;
            productService.UpdateProduct(product);

            //207 - items, 10 - shipping (fixed), 20 - payment fee, 23.7 - tax, [-3] - discount
            var scTotal = GetService<IOrderTotalCalculationService>().GetShoppingCartTotal(ShoppingCart, out var discountAmount, out var appliedDiscounts, out _, out _, out _);
            discountService.DeleteDiscount(discount);
            discount.DiscountType = DiscountType.AssignedToOrderSubTotal;
            TestPaymentMethod.AdditionalHandlingFee = 0M;

            product.IsFreeShipping = true;
            productService.UpdateProduct(product);

            scTotal.Should().Be(257.7M);
            discountAmount.Should().Be(3);
            appliedDiscounts.Count.Should().Be(1);
            appliedDiscounts.First().Name.Should().Be("Discount 1");
        }

        [Test]
        public void CanConvertRewardPointsToAmount()
        {
            rewardPointsSettings.Enabled = true;
            rewardPointsSettings.ExchangeRate = 15M;

            settingService.SaveSetting(rewardPointsSettings);

            GetService<IOrderTotalCalculationService>().ConvertRewardPointsToAmount(100).Should().Be(1500);
        }

        [Test]
        public void CanConvertAmountToRewardPoints()
        {
            rewardPointsSettings.Enabled = true;
            rewardPointsSettings.ExchangeRate = 15M;

            settingService.SaveSetting(rewardPointsSettings);
            //we calculate ceiling for reward points
            GetService<IOrderTotalCalculationService>().ConvertAmountToRewardPoints(100).Should().Be(7);
        }

        [Test]
        public void CanCheckMinimumRewardPointsToUseRequirement()
        {
            rewardPointsSettings.Enabled = true;
            rewardPointsSettings.MinimumRewardPointsToUse = 0;

            settingService.SaveSetting(rewardPointsSettings);

            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(0).Should().BeTrue();
            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(1).Should().BeTrue();
            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(10).Should().BeTrue();

            rewardPointsSettings.MinimumRewardPointsToUse = 2;
            settingService.SaveSetting(rewardPointsSettings);

            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(0).Should().BeFalse();
            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(1).Should().BeFalse();
            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(2).Should().BeTrue();
            GetService<IOrderTotalCalculationService>().CheckMinimumRewardPointsToUseRequirement(10).Should().BeTrue();
        }
    }
}
