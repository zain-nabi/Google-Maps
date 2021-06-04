using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Triton.Model.TritonExpress.Tables;
using Triton.Operations.Models;
using Triton.Service.Data;
using Triton.Service.Utils;
using Vendor.Services.CustomModels;
using Vendor.Services.Data;
using CustomerService = Vendor.Services.Data.CustomerService;
using TransportTypeService = Vendor.Services.Data.TransportTypeService;
using UserMapService = Vendor.Services.Data.UserMapService;

namespace Triton.Operations.Controllers
{
    [Authorize]
    public class QuotationController : Controller
    {
        //public async Task<IActionResult> Index()
        //{
        //    return View();
        //}
        [HttpGet]
        public async Task<IActionResult> Create(long? quoteId)
        {
            try
            {
                VendorQuoteModel model;

                if (quoteId.HasValue)
                    model = await QuoteService.GetQuoteByID(quoteId.Value);
                else
                {
                    model = new VendorQuoteModel
                    {
                        Quote = new Quotes { ServiceTypeID = 1 }
                    };
                }

                model.TransportTypes = await TransportTypeService.GetAllTransportTypes();
                model.AllowedCustomerList = (await UserMapService.GetUserCustomerMapModel(User.GetUserId())).Customers;
                model.AllowedSundries = await QuoteService.GetQuoteSurcharges();

                model.SundryDropDownList = new List<SundryList>();

                foreach (var item in model.AllowedSundries.OrderBy(x => x.Heading))
                {
                    model.SundryDropDownList.Add(new SundryList
                    {
                        Description = item.Description,
                        Value = item.OutChargeCode,
                        ChargeAmount = item.ChargeAmount,
                        Heading = item.Heading,
                        Selected = false
                    });
                }

                if (model.AllowedCustomerList.Count == 0)
                {
                    var cash = await CustomerService.GetCrmCustomerById(500);
                    model.AllowedCustomerList.Add(cash);
                }

                model.TransportTypes = model.TransportTypes.Where(f => f.TransportTypeID != 6).ToList();

                // Remove the white spaces from the telephone number
                if (model.Quote.SenTelNo != null)
                {
                    model.Quote.SenTelNo = model.Quote.SenTelNo.Replace(" ", "");
                }

                if (model.Quote.RecTelNo != null)
                {
                    model.Quote.RecTelNo = model.Quote.RecTelNo.Replace(" ", "");
                }

                return View(model);
            }
            catch
            {
                ViewData["Header"] = "404";
                ViewData["Message"] =
                    "We are experiencing a problem with the quotations.  Please contact Triton Express";
                return View("~/Views/Shared/Error.cshtml");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(VendorQuoteModel model)
        {

            if (ModelState.IsValid)
            {
                model.TransportTypes = await TransportTypeService.GetAllTransportTypes();
                //model.AllowedCustomerList = await _customers.GetCrmCustomersByRepUserId(250);
                //model.AllowedSundries = await _quotesApi.GetQuoteSurcharges();



                //Add QuoteLines to Model
                model.QuoteLines =
                    JsonConvert.DeserializeObject<List<QuoteLines>>(WebUtility.UrlDecode(model.QuoteLineHF) ??
                                                                    string.Empty);

                model.Quote.ServiceTypeText = model.TransportTypes
                    .Find(m => m.TransportTypeID == model.Quote.ServiceTypeID)?.Description.Trim();

                model.Quote.CreatedByTSUserID = User.GetUserId(); //GetTritonSecurityUserID();
                model.Quote.CreatedOn = DateTime.Now;

                // Add on the Sundry charges
                model.QuoteSundrys = new List<QuoteSundrys>();

                // Sundry charge / sundry service
                foreach (var item in model.SundryDropDownList)
                {
                    if (item.Selected)
                    {
                        var quoteSundryItem = new QuoteSundrys
                        {
                            SundryService = item.Value,
                            SundryCharge = item.ChargeAmount
                        };

                        model.QuoteSundrys.Add(quoteSundryItem);
                    }
                }

                //var response = await _quotesApi.PostQuoteUAT(model);
                 var response = await QuoteService.PostProduction(model);

                if (response.ReturnCode == "0000")
                {
                    ViewData["Header"] = "Successfully saved";
                    ViewData["Message"] =
                        $"Thank you for creating a quote.  We will provide a confirmation of this quotation.<h6>Quote No:  {response.Reference}</h6>";
                    ViewData["Url"] = $"{Request.Path}";

                    return View("~/Views/Shared/_Success.cshtml");
                    //return RedirectToAction("View", "Quotation", new { quoteId });
                }

                // Failed to create the quote
                ViewData["Header"] = "Oops";
                ViewData["Message"] = $"{response.ReturnCode} - {response.ReturnMessage}";

                return View("~/Views/Shared/Error.cshtml");
            }

            model.TransportTypes = await TransportTypeService.GetAllTransportTypes();
            model.AllowedCustomerList = await CustomerService.GetCrmCustomersByRepUserId(250);
            model.AllowedSundries = await QuoteService.GetQuoteSurcharges();
            model.SundryDropDownList = new List<SundryList>();

            foreach (var item in model.AllowedSundries)
            {
                model.SundryDropDownList.Add(new SundryList
                {
                    Description = item.Description,
                    Value = item.OutChargeCode,
                    Selected = false
                });
            }

            ModelState.AddModelError("Error",
                ModelState.Keys.SelectMany(key => ModelState[key].Errors).FirstOrDefault()?.ErrorMessage);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> View(long quoteId)
        {
            var quotationViewModel = new QuotationViewModel();
            quotationViewModel.VendorQuoteModel = await QuoteService.GetQuoteByID(quoteId);
            //TransportTypes = await _transportTypes.GetAllTransportTypes(),
            quotationViewModel.VendorQuoteModel.AllowedCustomerList = (await UserMapService.GetUserCustomerMapModel(User.GetUserId())).Customers;
            //AllowedSundries = await _quotesApi.GetQuoteSurcharges()            
            quotationViewModel.ReportUrl = "http://tiger/ReportServer/Pages/ReportViewer.aspx?/CRMReports/QuoteNewFormat&QuoteID=@quoteId&rs:ParameterLanguage=&rc:Parameters=Collapsed&rs:Command=Render&rs:Format=HTML4.0".Replace("@quoteId", quoteId.ToString());
            return View(quotationViewModel);

        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = new VendorQuoteSearchModel
            {
                AllowedCustomerList = (await UserMapService.GetUserCustomerMapModel(User.GetUserId())).Customers
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Index(VendorQuoteSearchModel model)
        {

            var dateSplit = model.FilterDate.Split("-");

            model.DateFrom = Convert.ToDateTime(dateSplit[0].Trim());
            model.DateTo = Convert.ToDateTime(dateSplit[1].Trim());

            var query = await QuoteService.GetQuotesbyCustomerIdOptRefandDates(model.CustomerID, model.QuoteRef,
                model.DateFrom, model.DateTo);
            query.AllowedCustomerList = (await UserMapService.GetUserCustomerMapModel(User.GetUserId())).Customers;
            query.ShowReport = true;
            return View(query);
        }

        public async Task<ActionResult> QuotePDF(int quoteId)
        {
            var x = await QuoteService.GetQuoteDocument(quoteId);

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = $"Quote.pdf",
                Inline = false,
            };
            return File(x.ImgData, "application/pdf");
        }

        public async Task<ActionResult> EmailQuote(int quoteId, string EmailAddress)
        {
            try
            {
                await QuoteService.EmailQuoteDocument(quoteId, EmailAddress);
            }
            catch
            {
                // ignored
            }

            return RedirectToAction("View", "Quotation", new { quoteId });
        }

        public async Task<ActionResult<List<PostalCodes>>> GetPostalCodes(string name)
        {
            return await PostalCodeService.GetPostalCodesOps(name);
        }

        public async Task<ActionResult<List<Customers>>> GetCustomerSearch(string search)
        {
            return await CustomerService.FindCrmCustomer(search);
        }

        public async Task<ActionResult<List<Vendor.Services.Freightware.PROD.GetSiteList.GetSiteListResponseSiteOutput>>> GetSiteList(string customerCode, string siteCode)
        {
            var x = await FreightwareService.GetSiteList(customerCode, siteCode);
            return JsonConvert.DeserializeObject<List<Vendor.Services.Freightware.PROD.GetSiteList.GetSiteListResponseSiteOutput>>(x.DataObject.ToString());
        }

        public async Task<ActionResult<List<Vendor.Services.Freightware.UAT.GetSiteList.GetSiteListResponseSiteOutput>>> GetSiteListUAT(string customerCode, string siteCode)
        {
            var x = await FreightwareService.GetSiteListUAT(customerCode, siteCode);
            return JsonConvert.DeserializeObject<List<Vendor.Services.Freightware.UAT.GetSiteList.GetSiteListResponseSiteOutput>>(x.DataObject.ToString());
        }
    }
}
