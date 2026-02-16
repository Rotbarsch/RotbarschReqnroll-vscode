Feature: strings get appended

Scenario Outline: Fun with strings
	When <left> is appended with <right>
	Then the result string should be <result>

Examples: 
	| left | right | result |
	| aa   | bb    | aabb   |
	| null | null  | null   |
	| null | bb    | bb     |
	| $(a)    | $(b)     | $(a)$(b)     |

