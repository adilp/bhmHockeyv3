import { captureRef } from 'react-native-view-shot';
import * as Sharing from 'expo-sharing';
import { readAsStringAsync, EncodingType } from 'expo-file-system/legacy';
import * as Clipboard from 'expo-clipboard';
import type { View } from 'react-native';

export async function captureRosterCard(cardRef: React.RefObject<View | null>): Promise<string> {
  const uri = await captureRef(cardRef, {
    format: 'png',
    quality: 1,
    result: 'tmpfile',
  });
  return uri;
}

export async function shareRosterImage(imageUri: string): Promise<void> {
  const isAvailable = await Sharing.isAvailableAsync();
  if (!isAvailable) {
    throw new Error('Sharing is not available on this device');
  }
  await Sharing.shareAsync(imageUri, { mimeType: 'image/png' });
}

export async function copyRosterToClipboard(imageUri: string): Promise<void> {
  const base64 = await readAsStringAsync(imageUri, {
    encoding: EncodingType.Base64,
  });
  await Clipboard.setImageAsync(base64);
}
