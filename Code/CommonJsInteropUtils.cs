using Microsoft.JSInterop;

namespace CsharpOnlineCompiler.Code
{
    public class JSInterop
    {
        //Constructors ///////////////////////////////////////////////

        public JSInterop(IJSRuntime jsr, object reference)
        {
            JSR = jsr;
            DotNetObjectRef = DotNetObjectReference.Create(reference);
        }

        //////////////////////////////////////////////////////////////


        //Properties /////////////////////////////////////////////////

        public IJSRuntime JSR { get; set; }
        public object DotNetObjectRef { get; set; }

        //////////////////////////////////////////////////////////////



        //Monaco Methods /////////////////////////////////////////////

        public async Task InitializeMonaco(string editorText, string language)
        {
            await JSR.InvokeVoidAsync("InitializeMonaco", editorText, language);
        }

        public async Task SetMonacoContent(string editorText)
        {
            await JSR.InvokeVoidAsync("SetMonacoContent", editorText);
        }

        //////////////////////////////////////////////////////////////



        //Methods ////////////////////////////////////////////////////


        public ValueTask SendObjectReference()
        {
            //Send this objects reference to javascript so we can make calls to C# instance methods from it.
            return JSR.InvokeVoidAsync("setDotNetObjRef", DotNetObjectRef);
        }



        public async Task DownloadFile(string fileName, string fileType, string value)
        {
            await JSR.InvokeVoidAsync("BlazorDownloadFile", "SourceCode.cs", "text/plain", value);
        }



        public async Task<bool> GetConfirmation(string text)
        {
            return await JSR.InvokeAsync<bool>("confirm", text);
        }


        public async Task CopyToClipboard(string text)
        {
            await JSR.InvokeVoidAsync("copyToClipboard", text);
        }



        public async Task StyleElementByID(string ID, string attribute, string value)
        {
            await JSR.InvokeVoidAsync("applyStyleForElement", new { id = ID, attrib = attribute, value });
        }

        public async Task StyleElementByClass(string className, string attribute, string value)
        {
            await JSR.InvokeVoidAsync("applyStyleForElementClass", new { className, attrib = attribute, value });
        }

        public async Task SetElementInnerHTMLByID(string ID, string value)
        {
            await JSR.InvokeVoidAsync("setInnerHTMLForElement", new { id = ID, value });
        }

        public async Task DisableElementByID(string ID, bool value)
        {
            await JSR.InvokeVoidAsync("disableElement", new { id = ID, value });
        }

        //////////////////////////////////////////////////////////////
    }
}
