Feature: A feature with booleans

Scenario Outline: Fun with bool
	When <left> OR <right>
	Then the result bool should be <result>

Examples:
	| left  | right | result |
	| true  | true  | true   |
	| true  | false | true   |
	| false | true  | true   |
	| false | false | false  |

