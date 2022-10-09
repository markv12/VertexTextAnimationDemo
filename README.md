# Vertex Text Animation Demo

Based on code from:

Batbarian (<a href="https://store.steampowered.com/app/837460/Batbarian_Testament_of_the_Primordials/">Steam</a>)

Kingdom of Night (<a href="https://store.steampowered.com/app/1094600/Kingdom_of_Night/">Steam</a>)

<a href="https://www.youtube.com/watch?v=So8DpNh3XOE">Video Tutorial</a>

Important Classes/Functions:

`DialogueManager.cs` Line 45 `PlayDialogue` Starts the text animation

`DialogueUtility.cs` Line 28 `ProcessInputString` Parses the text and pulls out any special tags/commands

`DialogueVertexAnimator.cs` Line 23 `AnimateTextIn` Takes the processed text and dialogue commands and performs the text vertex animation
