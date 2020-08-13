pipeline {
  agent any
  stages{
    stage('Build') {
      steps{
        echo "Building project"
        bat label: '', script: '"C:\\.Net\\nopCommerce-develop\\src\\Presentation\\Nop.Web.Framework\\Nop.Web.Framework.csproj"'
      }
    }
     stage('Archive') {
      steps{
        echo "Archiving project"
        archiveArtifacts artifacts: '**/*.sln', followSymlinks: false
      }
    }
  }
}
