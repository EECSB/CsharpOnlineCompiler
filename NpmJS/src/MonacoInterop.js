import * as monaco from 'monaco-editor';

let monacoEditor;
let dotNetObjRef;

window.setDotNetObjRef	= function (ref) {
	dotNetObjRef = ref;
}

window.sendTextToCSharp = function (text) {
	dotNetObjRef.invokeMethodAsync('EditorContentChanged', text);
}

window.InitializeMonaco = function (text, language) {
	monacoEditor = monaco.editor.create(document.getElementById('codeEditor'), {
		value: text,
		language: language,
		automaticLayout: true
	});

	monacoEditor.onDidChangeModelContent(function (event) {
		window.sendTextToCSharp(monacoEditor.getValue());
	});
}

window.SetMonacoContent = function (text) {
	monacoEditor.setValue(text);
}

window.GetMonacoContent = function () {
	return monacoEditor.getValue();
}