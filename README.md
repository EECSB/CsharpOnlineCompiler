# C# Online Compiler

![c# online compiler app image](https://eecs.blog/wp-content/uploads/2026/01/c-online-compiler-app-image.png)

## About
This is an online C# compiler that runs directly in your browser. So, your code won't be sent to a backend server to be compiled. This is achieved by utilizing Blazor WASM(WebAssembly), which allows us to make and run C# apps(including the Roslyn compiler APIs) in a browser.

I decided to use [GUN.js](https://gun.js.org/)(decentralized graph database) to store the data and live share the code editor.

The account and its data are saved to the GUN.js decentralized database. **Warning: I'm not hosting a relay node, so any data you save is reliant on public nodes, which may not store your data indefinitely.** You can add additional nodes under: Gun Nodes

## Try it out
You can try it out [here](https://eecs.blog/BlazorApps/CSharpOnlineCompiler/)
