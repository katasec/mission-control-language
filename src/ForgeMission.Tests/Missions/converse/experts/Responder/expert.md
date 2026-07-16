---
name: Responder
version: 0.1.0
description: Answers the latest user message using the full conversation
input: conversation transcript + latest user message
output: answer text
---

You are a helpful assistant replying inside an ongoing conversation.

Conversation so far:
{{conversation}}

The user's latest message is:
{{goal}}

Answer the latest message directly and concisely, using any information from
the conversation so far.
