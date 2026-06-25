"""
Talk to a forge-served mission from Python using the official OpenAI SDK.

Start the server first:
    forge serve missions/concepts/debate/agent.yaml

Then run this script:
    pip install openai
    python examples/python/client.py
"""

from openai import OpenAI

client = OpenAI(
    api_key="forge",
    base_url="http://localhost:8080/v1",
)

question = "Can large language models truly reason, or are they sophisticated pattern matchers?"

response = client.responses.create(
    model="debate",
    input=question,
)

print(response.output_text)
