# Gemini-Live-Unity

This is a repository that contains scripts from a Unity Project for integrating [Gemini-Live](https://ai.google.dev/gemini-api/docs/live-api/get-started-websocket) API with Unity through raw websockets. 

Here's a quick video that shows these scripts in Unity: [Watch the YouTube Demo - PENDING VIDEO]().

# Setup

### Step 1: Login to your Google account, copy your secret key and save it for the next stage
You will need to fetch your Google API key, which can be found in your Google AI Studio account under `Get API Key` (or through [this direct link](https://aistudio.google.com/app/apikey)). 

### Step 2: Add the scripts to your scene inside an empty object

![](/Images/Gemini_Live_Image_1.png)

Attach the dependencies inside the variables, it should ended up like the picture. 

### Step 3: Add a simplified User Interface 
The interface should include a Canvas, a Text, an Input Field, and a Button.

![](/Images/Gemini_Live_Image_2.png)

# How it works?

Unlike a standard API call, which sends a request and waits for a complete response, this client maintains a persistent connection with Gemini. Messages are streamed in chunks as they are generated, which means lower latency and a more natural conversation. Once connected, the client can send and receive multiple message types simultaneously over a single WebSocket channel.
