# 会社アカウント
function gitCo() {
    git config --global user.name "[yagi_hiroki]"   # ***には会社アカウントのusername
    git config --global user.email "[yagi_hiroki@live2d.com]"  # ***には会社アカウントのemailアドレス
    echo "Switched to Company Account"　　　　　　　　　　　　#アカウントを切り替えると表示される内容
}

# 個人アカウント
function gitPe() {
    git config --global user.name "[isu5028]"   # ***には個人用のusername
    git config --global user.email "[yagi16k@gmail.com]"  # ***には個人用のemailアドレス
    echo "Switched to Personal Account"　　　　　　　　　　#アカウントを切り替えると表示される内容
}