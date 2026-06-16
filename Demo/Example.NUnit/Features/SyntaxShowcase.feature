Feature: Syntax showcase

# This feature file demonstrates all Gherkin syntax elements:
# Background, comments, docstrings, double-quoted strings and tags.

Background: Common setup
	Given the system is ready

@syntax @showcase
Scenario: Appending "hello" and "world" strings
	When hello is appended with world
	Then the result string should be helloworld

# The next scenario demonstrates docstring arguments

Scenario: Docstring example
	Given the following message:
	"""
	Hello from docstring
	"""
	Then the message should be:
	"""
	Hello from docstring
	"""

Scenario: Simple addition after background
	When I add 1 and 2
	Then the result should be 3
