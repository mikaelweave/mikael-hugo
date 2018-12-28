---
title: "Seemlessly Run RDP Applications Locally"
date: "2018-05-17"
tags: []
categories: []
draft: false
featured_image: "2018-05-17-12_22_21-.png"
---

**My Problem:**

A lot of companies (including where I work) (edit - used to work) require their developers to code on virtual machines that have elevated access vs your local machine. The idea is this reduces the footprint of computers and accounts that have access to critical systems in case they are compromised. We browse the internet and access email locally and develop and access databases through our VMs. If you’re thinking “that sounds like a pain in the butt” you are mostly right.

My workstation is usually three 1080p-ish monitors. Spanning a RDP session over part of my workstation and leaving space for web activities has been okay but I’ve always been dissatisfied. I have used a custom sized RDP profile which spans two of three monitors leaving one for email and instant messages. RDP doesn’t allow full screen on a subset of monitors, though, so it’s a fixed sized window that I (attempt to) position just right ot maximize my development area.

There has to be a better way…

**My Solution:**

Microsoft has a technology called RemoteApp programs. It’s not documented very well, but it’s a way to run remote applications in a locally drawn window seamlessly without being anchored to a RDP window. There are some [PowerShell tools](https://docs.microsoft.com/en-us/powershell/module/remotedesktop/new-rdremoteapp?view=win10-ps) and [registry hacks](http://geekswithblogs.net/twickers/archive/2009/12/18/137048.aspx) to create and manage these remote apps, however, I recently ran across a lovely piece of software called [RemoteApp Tool by Kim Knight](http://www.kimknight.net/remoteapptool). It helps you create RemoteApp programs easily. It only works on Server, Professional, Enterprise, and Education versions of Windows due to the limitations of Terminal Services.

**Attempt 1:**

On my remote system, I installed RemoteApp Tool, opened it up, and added a new RemoteApp for Visual Studio (Preview, because I ride on the bleeding edge).

{{< imgproc "2018-05-17-11_51_06-Clipboard.png" "RemoteApp Tool Screen Attempt 1" >}}

Now with the RemoteApp created, I clicked “Create Client Connection” to make a RDP file for my local computer. Leaving everything at the defaults, it spits out a RDP file!

{{< imgproc "2018-05-17-12_00_06-spf-it-dev1366-Remote-Desktop-Connection.png" "Another RemoteApp Tool Screen" >}}

After copying the RDP file to my local machine, I had Visual Studio running seamlessly next to all my local applications! Sweet!

{{< imgproc "2018-05-17-12_04_26-OES.SVC-Microsoft-Visual-Studio-Preview-Administrator-Remote.png" "Visual Studio Running Remotely" >}}

I enjoyed my success until I tried to launch two instances of Visual Studio (one for our service solution, one for our web app) and got the following error:  “Another user is signed in. If you continue, they’ll be disconnected. Do you want to sign in anyway?”
Crap, I guess I am still remoting into a Windows 10 machine which is only licensed for one RDP connection.

**Attempt 2:**

I noticed while playing around in the Visual Studio RemoteApp, when I debug my application it launches a web browser in a new window under the same Remote App. What if I could make a “RemoteApp Launcher” to open whatever I want under one session?? Back to RemoteApp Tool I go…

What if I just made a RemoteApp to a specific explorer window with all my application shortcuts?

{{< imgproc "2018-05-17-12_13_54-spf-it-dev1366-Remote-Desktop-Connection.png" "Another RemoteApp Tool Screen" >}}

After generating the RDP file (just using the defaults for “Create Client Connection” in RemoteApp Tool) and copying it to my local machine, I now had a “launcher” to open multiple remote applications locally under one RemoteApp and this one RDP session.

{{< imgproc "2018-05-17-12_19_53-Windows-PowerShell.png" "Explorer Running Remotely" >}}

Your local taskbar even shows you which windows are from the RemoteApp and which are local with an overlay.

{{< imgproc "2018-05-17-12_22_21-.png" "Taskbar with Remote Icons" >}}

I still have an occasional glitch (usually just connection issues), but I’m much happier developing on my remote VM this way.