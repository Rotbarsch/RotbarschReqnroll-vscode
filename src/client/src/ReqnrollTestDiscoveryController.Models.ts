
export interface DiscoverTestsParams {
  uri: string;
}

export interface DiscoveredTest {
  id: string;
  label: string;
  uri: string;
  range: TestRange;
  children?: DiscoveredTest[];
}

export interface TestRange {
  startLine: number;
  startCharacter: number;
  endLine: number;
  endCharacter: number;
}


