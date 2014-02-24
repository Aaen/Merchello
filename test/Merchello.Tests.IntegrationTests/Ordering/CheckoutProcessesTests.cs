﻿using System;
using System.Linq;
using Merchello.Core;
using Merchello.Core.Gateways.Shipping;
using Merchello.Core.Gateways.Shipping.FixedRate;
using Merchello.Core.Gateways.Taxation.FixedRate;
using Merchello.Core.Models;
using Merchello.Core.Services;
using Merchello.Tests.IntegrationTests.TestHelpers;
using Merchello.Web;
using NUnit.Framework;

namespace Merchello.Tests.IntegrationTests.Ordering
{
    [TestFixture]
    public class CheckoutProcessesTests : MerchelloAllInTestBase
    {
        private IProduct _product1;
        private IProduct _product2;
        private IProduct _product3;
        private IProduct _product4;
        private IProduct _product5;


        [TestFixtureSetUp]
        public override void FixtureSetup()
        {
            base.FixtureSetup();

            DbPreTestDataWorker.DeleteAllShipCountries();

            #region Back Office

            #region Product Entry

            _product1 = DbPreTestDataWorker.MakeExistingProduct(true, 1, 1);
            _product2 = DbPreTestDataWorker.MakeExistingProduct(true, 1, 2);
            _product3 = DbPreTestDataWorker.MakeExistingProduct(true, 1, 3);
            _product4 = DbPreTestDataWorker.MakeExistingProduct(true, 1, 4);
            _product5 = DbPreTestDataWorker.MakeExistingProduct(true, 1, 5);


            #endregion

            #region WarehouseCatalog

            var defaultCatalog = DbPreTestDataWorker.WarehouseService.GetDefaultWarehouse().WarehouseCatalogs.FirstOrDefault();
            if (defaultCatalog == null) Assert.Ignore("Default WarehouseCatalog is null");

            #endregion // WarehouseCatalog

            #region Settings -> Shipping

            // Add countries (US & DK) to default catalog
            #region Add Countries

            var us = MerchelloContext.Current.Services.StoreSettingService.GetCountryByCode("US");
            var usCountry = new ShipCountry(defaultCatalog.Key, us);
            ((ServiceContext)MerchelloContext.Current.Services).ShipCountryService.Save(usCountry);

            var dk = MerchelloContext.Current.Services.StoreSettingService.GetCountryByCode("DK");
            var dkCountry = new ShipCountry(defaultCatalog.Key, dk);
            ((ServiceContext)MerchelloContext.Current.Services).ShipCountryService.Save(dkCountry);

            #endregion // ShipCountry

            #region Add a GatewayProvider (RateTableShippingGatewayProvider)

            var key = Constants.ProviderKeys.Shipping.FixedRateShippingProviderKey;
            var rateTableProvider = (FixedRateShippingGatewayProvider)MerchelloContext.Current.Gateways.Shipping.ResolveByKey(key);

            #region Add and configure 3 rate table shipmethods

            var gwshipMethod1 = (FixedRateShipMethod)rateTableProvider.CreateShipMethod(FixedRateShipMethod.QuoteType.VaryByPrice, usCountry, "Ground (Vary by Price)");
            gwshipMethod1.RateTable.AddRow(0, 10, 25);
            gwshipMethod1.RateTable.AddRow(10, 15, 30);
            gwshipMethod1.RateTable.AddRow(15, 25, 35);
            gwshipMethod1.RateTable.AddRow(25, 60, 40); // total price should be 50M so we should hit this tier
            gwshipMethod1.RateTable.AddRow(25, 10000, 50);
            rateTableProvider.SaveShipMethod(gwshipMethod1);

            var gwshipMethod2 = (FixedRateShipMethod)rateTableProvider.CreateShipMethod(FixedRateShipMethod.QuoteType.VaryByWeight, usCountry, "Ground (Vary by Weight)");
            gwshipMethod2.RateTable.AddRow(0, 10, 5);
            gwshipMethod2.RateTable.AddRow(10, 15, 10); // total weight should be 10M so we should hit this tier
            gwshipMethod2.RateTable.AddRow(15, 25, 25);
            gwshipMethod2.RateTable.AddRow(25, 10000, 100);
            rateTableProvider.SaveShipMethod(gwshipMethod2);

            var gwshipMethod3 = (FixedRateShipMethod)rateTableProvider.CreateShipMethod(FixedRateShipMethod.QuoteType.VaryByPrice, dkCountry, "Ground (Vary by Price)");
            gwshipMethod3.RateTable.AddRow(0, 10, 5);
            gwshipMethod3.RateTable.AddRow(10, 15, 10);
            gwshipMethod3.RateTable.AddRow(15, 25, 25);
            gwshipMethod3.RateTable.AddRow(25, 60, 30); // total price should be 50M so we should hit this tier
            gwshipMethod3.RateTable.AddRow(25, 10000, 50);
            rateTableProvider.SaveShipMethod(gwshipMethod3);

            #endregion // ShipMethods

            #endregion // GatewayProvider

            #endregion  // Shipping

            #region Settings -> Taxation

            var provider = MerchelloContext.Current.Gateways.Taxation.ResolveByKey(Constants.ProviderKeys.Taxation.FixedRateTaxationProviderKey);
            
            provider.DeleteAllTaxMethods();

            var taxMethod = provider.CreateTaxMethod("US", 0);

            taxMethod.Provinces["WA"].PercentRateAdjustment = 8.7M;

            provider.SaveTaxMethod(taxMethod);
    
            
            #endregion

            #endregion  // Back Office

        }

        [SetUp]
        public void Init()
        {
            
        }

        //[Test]
        //public void SetupForExampleSite()
        //{

        //}

        /// <summary>
        /// Test verifies that a simple checkout scenario
        /// </summary>
        [Test]
        public void Can_Complete_Simple_Checkout_Scenario()
        {
            // The basket is empty
            WriteBasketInfoToConsole();

            #region Customer Does Basket Operations

            // -------------------------
            // Add a couple of products
            // -------------------------
            Console.WriteLine("Adding 10 'Product1' to the Basket");
            CurrentCustomer.Basket().AddItem(_product1, 10);

            Console.WriteLine("Adding 2 'Product2' to the Basket");
            CurrentCustomer.Basket().AddItem(_product2, 2);
            CurrentCustomer.Basket().Save();
            
            WriteBasketInfoToConsole();
            Assert.AreEqual(12, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(14, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(2, CurrentCustomer.Basket().TotalItemCount);

            // -------------------------
            // Add another product2
            // -------------------------
            Console.WriteLine("Adding another 'Product2' to the Basket");
            CurrentCustomer.Basket().AddItem(_product2);
            CurrentCustomer.Basket().Save();

            WriteBasketInfoToConsole();
            Assert.AreEqual(13, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(16, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(2, CurrentCustomer.Basket().TotalItemCount);

            // -------------------------
            // Add products - product3 and product4
            // -------------------------
            Console.WriteLine("Adding 2 'Product4' to the Basket");
            CurrentCustomer.Basket().AddItem(_product4, 2);

            WriteBasketInfoToConsole();
            Assert.AreEqual(15, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(24, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(3, CurrentCustomer.Basket().TotalItemCount);

            Console.WriteLine("Adding 1 'Product3' to the Basket");
            CurrentCustomer.Basket().AddItem(_product3, 1);
            CurrentCustomer.Basket().Save();

            WriteBasketInfoToConsole();
            Assert.AreEqual(16, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(27, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(4, CurrentCustomer.Basket().TotalItemCount);

            // -------------------------
            // Update Product4's quantity to 1
            // -------------------------
            CurrentCustomer.Basket().Items.First(x => x.Sku == _product4.Sku).Quantity = 1;
            CurrentCustomer.Basket().Save();

            WriteBasketInfoToConsole();
            Assert.AreEqual(15, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(23, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(4, CurrentCustomer.Basket().TotalItemCount);

            // -------------------------
            // Remove Product3 from the basket
            // -------------------------
            CurrentCustomer.Basket().RemoveItem(_product3.Sku);
            CurrentCustomer.Basket().Save();

            WriteBasketInfoToConsole();
            Assert.AreEqual(14, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(20, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(3, CurrentCustomer.Basket().TotalItemCount);


            #endregion


            #region CheckOut

            // ------------- Customer Shipping Address Entry -------------------------

            // Customer enters the destination shipping address
            var destination = new Address()
                {
                    Name = "Mindfly Web Design Studio",
                    Address1 = "115 W. Magnolia St.",
                    Address2 = "Suite 504",
                    Locality = "Bellingham",
                    Region = "WA",
                    PostalCode = "98225",
                    CountryCode = "US"
                };

            // Assume customer selects billing address is same as shipping address
            CurrentCustomer.Basket().OrderPreparation().SaveBillToAddress(destination);

            // --------------- ShipMethod Selection ----------------------------------

            // Package the shipments 
            //
            // This should return a collection containing a single shipment
            //
            var shipments = CurrentCustomer.Basket().PackageBasket(destination).ToArray();

            Assert.IsTrue(shipments.Any());
            Assert.AreEqual(1, shipments.Count());

            var shipment = shipments.First();

            // Get a shipment rate quote
            //
            // This should return a collection containing 2 shipment rate quotes (for US)
            //
            var shipRateQuotes = shipment.ShipmentRateQuotes().ToArray();

            foreach (var srq in shipRateQuotes)
            {
                WriteShipRateQuote(srq);
            }

            // Customer chooses the cheapest shipping rate
            var approvedShipRateQuote = shipRateQuotes.FirstOrDefault();

            // start the Checkout process
            Assert.AreEqual(CurrentCustomer.Basket().TotalItemCount, CurrentCustomer.Basket().OrderPreparation().ItemCache.Items.Count);

            CurrentCustomer.Basket().OrderPreparation().SaveShipmentRateQuote(approvedShipRateQuote);

            // Customer changes their mind and adds Product 5 to the basket
            CurrentCustomer.Basket().AddItem(_product5);
            CurrentCustomer.Basket().Save();

            WriteBasketInfoToConsole();
            Assert.AreEqual(15, CurrentCustomer.Basket().TotalQuantityCount);
            Assert.AreEqual(25, CurrentCustomer.Basket().TotalBasketPrice);
            Assert.AreEqual(4, CurrentCustomer.Basket().TotalItemCount);

            // This should have cleared the CheckoutPreparation and reconstructed so that it matches the basket again
            Assert.AreEqual(CurrentCustomer.Basket().TotalItemCount, CurrentCustomer.Basket().OrderPreparation().ItemCache.Items.Count(x => x.LineItemType == LineItemType.Product));
            Console.WriteLine("CheckoutPrepartion was cleared!");

            // Because the customer went back and added another item the checkout workflow needs to 
            // be restarted

            // User is finally finished and going to checkout
            #region Final Checkout Prepartion 

            #region Shipping information

            // Save the billing information (again - the same as shipping information)
            CurrentCustomer.Basket().OrderPreparation().SaveBillToAddress(destination);

            shipments = CurrentCustomer.Basket().PackageBasket(destination).ToArray();
            Assert.IsTrue(shipments.Any());

            shipment = shipments.First();
            
            // shipment should have all four items packaged in it since they all were marked shippable
            Assert.AreEqual(CurrentCustomer.Basket().TotalItemCount, shipment.Items.Count, "Shipment did not contain all of the items");

            var shipmentRateQuotes = shipment.ShipmentRateQuotes().ToArray();
            Assert.AreEqual(2, shipmentRateQuotes.Count());

            
            // customer picks faster delivery so picks the more expensive rate from a drop down
            var dropDownListValue = shipmentRateQuotes.Last().ShipMethod.Key.ToString();

            var approvedShipmentRateQuote = shipment.ShipmentRateQuoteByShipMethod(dropDownListValue);

            // The shipment in the rate quote should have all four items packaged in it since they all were marked shippable 
            Assert.AreEqual(CurrentCustomer.Basket().Items.Count, approvedShipmentRateQuote.Shipment.Items.Count);

            Assert.NotNull(approvedShipRateQuote);
            WriteShipRateQuote(approvedShipmentRateQuote);
            
            // save the rate quote 
            CurrentCustomer.Basket().OrderPreparation().SaveShipmentRateQuote(approvedShipmentRateQuote);


            #endregion // end shipping info round 2

            // generate an invoice to preview
            var invoice = CurrentCustomer.Basket().OrderPreparation().GenerateInvoice();
            WriteInvoiceInfoToConsole(invoice);

            #endregion // completed checkout preparation

            #endregion

        }


        private void WriteBasketInfoToConsole()
        {
            Console.WriteLine("----------- Basket Item Info ---------------------");
            Console.WriteLine("Total quantity count: {0}", CurrentCustomer.Basket().TotalQuantityCount);
            Console.WriteLine("Total basket price: {0}", CurrentCustomer.Basket().TotalBasketPrice);
            Console.WriteLine("Total item count: {0}", CurrentCustomer.Basket().TotalItemCount);
            
        }

        private void WriteInvoiceInfoToConsole(IInvoice invoice)
        {
            Console.WriteLine("----------- Invoice Item Info ---------------------");
            foreach (var lineItem in invoice.Items)
            {
                Console.WriteLine("{0} - Quantity: {1} - Price: {2} = Total : {3} (Tax: {4})", lineItem.Name, lineItem.Quantity, lineItem.Price, lineItem.TotalPrice, lineItem.ExtendedData.GetValue(Constants.ExtendedDataKeys.LineItemTaxAmount));
            }
            Console.WriteLine("");

            Console.WriteLine("Total invoice price: {0}", invoice.Total);

            Console.WriteLine("Tax break down");
            Console.WriteLine("Base tax: {0}", invoice.Items.First(x => x.LineItemType == LineItemType.Tax).ExtendedData.GetValue(Constants.ExtendedDataKeys.BaseTaxRate));
            Console.WriteLine("Province tax (WA): {0}", invoice.Items.First(x => x.LineItemType == LineItemType.Tax).ExtendedData.GetValue(Constants.ExtendedDataKeys.ProviceTaxRate));
        }

        private void WriteShipRateQuote(IShipmentRateQuote srq)
        {
            Console.WriteLine("---------- Shipment Rate Quote ---------------------");
            Console.WriteLine("Name: {0}", srq.ShimpentLineItemName());
            Console.WriteLine("Rate Quote: {0}", srq.Rate);
            
        }
    }
}