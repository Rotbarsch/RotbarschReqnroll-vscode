Feature: Numeric Outline

Scenario Outline: Addition with Examples
	When I add <summand1> and <summand2>
	Then the result should be <sum>

Examples:
	| summand1 | summand2 | sum  |
	|       10 |       20 |   30 |
	|       10 |       20 |   30 |
	|       20 |       20 |   40 |
	|     20.5 |     20.4 | 40.9 |
	|     20.5 | null     | 20.5 |

