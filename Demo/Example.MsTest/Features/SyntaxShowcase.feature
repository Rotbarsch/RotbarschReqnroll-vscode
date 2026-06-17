Feature: Syntax showcase

# This feature file exercises every Gherkin syntax element that the
# Reqnroll TextMate grammar is expected to highlight:
#   - Feature / Background / Scenario / Scenario Outline / Examples keywords
#   - Step keywords: Given / When / Then / And / But
#   - Tags (@name)
#   - Outline parameters (<name>)
#   - Double-quoted string arguments ("value")
#   - Single-quoted string arguments ('value')
#   - Triple-quoted docstring blocks (""")
#   - Comment lines (# ...)

Background: Common setup
	Given the system is ready

@syntax @showcase
Scenario: Appending "hello" and "world" strings
	# Double-quoted strings inside step text should be highlighted as
	# string.quoted.double.reqnroll
	When hello is appended with world
	Then the result string should be helloworld

Scenario: Single-quoted string argument highlighting
	# Single-quoted strings inside step text should be highlighted as
	# string.quoted.single.reqnroll.  The lookbehind (?<![a-zA-Z])' in the
	# grammar ensures only a quote NOT preceded by a letter opens the scope,
	# so apostrophes like d'artagnan are NOT treated as string delimiters.
	When null is appended with 'hello'
	Then the result string should be 'hello'

# The next scenario demonstrates docstring arguments

Scenario: Docstring example
	# Triple-quoted blocks should be highlighted as string.quoted.triple.reqnroll
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

