﻿using BlackBarLabs.Extensions;
using EastFive.Collections.Generic;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using EastFive.Serialization;
using System.Net.NetworkInformation;
using EastFive.Extensions;

namespace EastFive.Security.SessionServer.Modules
{
    public class SpaHandlerModule : IHttpModule
    {
        internal const string IndexHTMLFileName = "index.html";

        private Dictionary<string, byte[]> lookupSpaFile;
        static internal byte[] indexHTML;

        public void Dispose()
        {
            //throw new NotImplementedException();
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += CheckForAssetMatch;
            
        }

        private void ExtractSpaFiles(HttpRequest request)
        {
            var spaZipPath = System.Web.Hosting.HostingEnvironment.MapPath("~/Spa.zip");
            using (var zipArchive = ZipFile.OpenRead(spaZipPath))
            {

                indexHTML = zipArchive.Entries
                    .First(item => string.Compare(item.FullName, IndexHTMLFileName, true) == 0)
                    .Open()
                    .ToBytes();

                var siteLocation = $"{request.Url.Scheme}://{request.Url.Authority}";
                lookupSpaFile = zipArchive.Entries
                    .Where(item => string.Compare(item.FullName, IndexHTMLFileName, true) != 0)
                    .Select(
                        entity =>
                        {
                            if (!entity.FullName.EndsWith(".js"))
                                return entity.FullName.PairWithValue(entity.Open().ToBytes());

                            var fileBytes = entity.Open()
                                .ToBytes()
                                .GetString()
                                .Replace("8FCC3D6A-9C25-4802-8837-16C51BE9FDBE.example.com", siteLocation)
                                .GetBytes();

                            return entity.FullName.PairWithValue(fileBytes);

                        })
                    .ToDictionary();
            }
        }

        private void CheckForAssetMatch(object sender, EventArgs e)
        {
            var httpApp = (HttpApplication)sender;
            var context = httpApp.Context;
            string filePath = context.Request.FilePath;
            string fileName = VirtualPathUtility.GetFileName(filePath);

            if (lookupSpaFile.IsDefault())
                ExtractSpaFiles(context.Request);

            if (lookupSpaFile.ContainsKey(fileName))
            {
                if (fileName.EndsWith(".js"))
                    context.Response.Headers.Add("content-type", "text/javascript");
                if (fileName.EndsWith(".css"))
                    context.Response.Headers.Add("content-type", "text/css");

                context.Response.BinaryWrite(lookupSpaFile[fileName]);
                HttpContext.Current.ApplicationInstance.CompleteRequest();
                return;
            }

            // TODO: Fix something like this
            HttpContextBase httpContextBase = new HttpContextWrapper(context);
            if (System.Web.Routing.RouteTable.Routes
                .SelectMany(
                    route => route.GetRouteData(httpContextBase).Values.Select(kvp => kvp.Key))
                .Where(
                    route => httpApp.Request.Path.StartsWith(route))
                .Any())
            {
                return;
            }

            if (httpApp.Request.Path.StartsWith("/api"))
                return;
            if (httpApp.Request.Path.StartsWith("/aadb2c"))
                return;
            if (httpApp.Request.Path.StartsWith("/content"))
                return;

            context.Response.Write(Properties.Resources.indexPage);
            HttpContext.Current.ApplicationInstance.CompleteRequest();

            //HttpContext.Current.RemapHandler(new AssetHandler(lookupSpaFile[fileName]));

        }

        //private class AssetHandler : IHttpHandler
        //{
        //    private byte[] file;

        //    public AssetHandler(byte[] file)
        //    {
        //        this.file = file;
        //    }

        //    public bool IsReusable => throw new NotImplementedException();

        //    public void ProcessRequest(HttpContext context)
        //    {
        //        context.

        //        context.Response = new HttpResponse(file);
        //    }
        //}

    }
}
