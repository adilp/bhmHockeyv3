// Mock for React Native
// This avoids importing the actual react-native package which has Flow types

module.exports = {
  Platform: {
    OS: 'ios',
    select: (obj) => obj.ios || obj.default,
  },
  StyleSheet: {
    create: (styles) => styles,
    flatten: (styles) => styles,
  },
  View: 'View',
  Text: 'Text',
  TouchableOpacity: 'TouchableOpacity',
  ScrollView: 'ScrollView',
  TextInput: 'TextInput',
  Image: 'Image',
  Animated: {
    View: 'Animated.View',
    Text: 'Animated.Text',
    createAnimatedComponent: (c) => c,
    timing: () => ({ start: jest.fn() }),
    spring: () => ({ start: jest.fn() }),
    Value: jest.fn(() => ({
      setValue: jest.fn(),
      interpolate: jest.fn(),
    })),
  },
  Dimensions: {
    get: () => ({ width: 375, height: 812 }),
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
  },
  NativeModules: {},
  useColorScheme: () => 'light',
};
